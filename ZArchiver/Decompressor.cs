using NLog;
using System;
using System.IO;
using System.IO.Compression;

namespace ZArchiver
{
    public class Decompressor : BaseWorker
    {
        public Decompressor(int blockSize, int maxThreadCount) : base(blockSize, maxThreadCount)
        {
            _logger = LogManager.GetCurrentClassLogger();
        }

        public void Decompress(string filename, string outputFile)
        {
            _logger.Info("Start decompressing file {0} to {1}", filename, outputFile);
            try
            {
                DoWork(filename, outputFile);
            }
            catch (Exception e)
            {
                _logger.Error(e, string.Format("An error occurred while decompressing the file {0}", filename));
            }
        }

        protected override BlockInfo[] GetBlockInfo(string path)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream))
            {
                var length = reader.ReadInt32();
                var blocksInfo = new BlockInfo[length];
                var offset = (length + 1) * sizeof(Int32);
                for (int i = 0; i < length; ++i)
                {
                    var blockSize = reader.ReadInt32();
                    blocksInfo[i] = new BlockInfo(GetPartName(path, i), blockSize, offset);
                    offset += blockSize;
                }
                return blocksInfo;
            }
        }

        protected override Boolean PrepareBlock(string path, BlockInfo block)
        {
            try
            {
                using (var compressedFileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var gzip = new GZipStream(compressedFileStream, CompressionMode.Decompress))
                using (var fileStream = new FileStream(block.FileName, FileMode.OpenOrCreate, FileAccess.Write))
                {
                    compressedFileStream.Position = block.Offset;
                    gzip.CopyTo(fileStream);
                    fileStream.Flush();
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
                    for (int i = 0; i < blockInfo.Length; ++i)
                    {
                        var buff = File.ReadAllBytes(blockInfo[i].FileName);
                        File.Delete(blockInfo[i].FileName);
                        writer.Write(buff);
                        writer.Flush();
                    }
                }
                _logger.Info("Decompressing file completed successfully");
            }
            catch (Exception e)
            {
                CleanBlocks(blockInfo);
                CleanOutputFile(output);
                _logger.Error(e, "An error occurred while combining parts of decompressed file");
            }
        }

        private readonly ILogger _logger;
    }
}
