using NLog;
using System;
using System.IO;
using System.IO.Compression;

namespace ZArchiver
{
    public class Compressor : BaseWorker
    {
        public Compressor(int blockSize, int maxThreadCount) : base(blockSize, maxThreadCount)
        {
            _logger = LogManager.GetCurrentClassLogger();
        }

        public void Compress(string filename, string outputFile)
        {
            try
            {
                _logger.Info("Start compressing file {0}", filename);
                DoWork(filename, outputFile);
            }
            catch (Exception e)
            {
                _logger.Error(e, string.Format("An error occurred while compressing the file {0}", filename));
            }
        }

        protected override BlockInfo[] GetBlockInfo(string path)
        {
            var fileInfo = new FileInfo(path);
            double fileSize = fileInfo.Length / (_blockSize * 1f);
            var blocksCount = (int)Math.Ceiling(fileSize);
            var blockInfo = new BlockInfo[blocksCount];
            for (int i = 0; i < blocksCount - 1; ++i)
            {
                blockInfo[i] = new BlockInfo(GetPartName(path, i), _blockSize, i * _blockSize);
            }

            var name = GetPartName(path, blocksCount - 1);
            var size = (int)(fileInfo.Length - (blocksCount - 1) * _blockSize);
            var offset = (blocksCount - 1) * _blockSize;
            blockInfo[blocksCount - 1] = new BlockInfo(name, size, offset);
            return blockInfo;
        }

        protected override Boolean PrepareBlock(string path, BlockInfo block)
        {
            try
            {
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var compressedFileStream = new FileStream(block.FileName, FileMode.OpenOrCreate))
                using (var gzip = new GZipStream(compressedFileStream, CompressionMode.Compress))
                {
                    fileStream.Position = block.Offset;
                    var buffer = new byte[block.Size];
                    fileStream.Read(buffer, 0, block.Size);
                    gzip.Write(buffer, 0, block.Size);
                }
                return true;
            }
            catch (Exception e)
            {
                _logger.Error(e, string.Format("An error occurred while preparing block (offset = {0}, size = {1}, filename = {2}) of compressed file", block.Offset, block.Size, block.FileName));
                return false;
            }
        }

        protected override void CombineBlocks(string output, BlockInfo[] blockInfo)
        {
            try
            {
                using (var outputStream = new FileStream(output, FileMode.OpenOrCreate))
                using (var writer = new BinaryWriter(outputStream))
                {
                    writer.Write(blockInfo.Length);
                    var sizeCursor = sizeof(Int32);
                    var blocksCursor = sizeCursor + blockInfo.Length * sizeof(Int32);
                    for (int i = 0; i < blockInfo.Length; ++i)
                    {
                        var buff = File.ReadAllBytes(blockInfo[i].FileName);
                        File.Delete(blockInfo[i].FileName);

                        outputStream.Position = sizeCursor;
                        writer.Write(buff.Length);
                        writer.Flush();
                        sizeCursor += sizeof(Int32);

                        outputStream.Position = blocksCursor;
                        writer.Write(buff);
                        writer.Flush();
                        blocksCursor += buff.Length;
                    }
                }
                _logger.Info("Compressing file completed successfully");
            }
            catch (Exception e)
            {
                CleanBlocks(blockInfo);
                CleanOutputFile(output);
                _logger.Error(e, "An error occurred while combining parts of compressed file");
            }
        }

        private readonly ILogger _logger;
    }
}
