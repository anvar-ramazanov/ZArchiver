using NLog;
using System;
using System.IO;
using System.IO.Compression;

namespace ZArchiver
{
    public class Compressor : ZArchiver
    {
        public Compressor(int blockSize, int maxThreadCount) : base(blockSize, maxThreadCount)
        {
            _logger = LogManager.GetCurrentClassLogger();
        }

        public override void Launch(string filename, string outputfile)
        {
            try
            {
                _logger.Info("Start compressing file {0}", filename);
                _writeDataOffset = 0;
                _writeHeaderOffset = 0;
                DoJob(filename, outputfile);
            }
            catch (Exception e)
            {
                _logger.Error(e, string.Format("An error occurred while compressing the file {0}", filename));
            }
        }

        protected override BlocksQueue GetReadQueue(string path)
        {
            var fileInfo = new FileInfo(path);
            double fileSize = fileInfo.Length / (_blockSize * 1f);
            var blocksCount = (int)Math.Ceiling(fileSize);
            var blocksQueue = new BlocksQueue(blocksCount);
            for (long i = 0; i < blocksCount - 1; ++i)  // long because avoid unpacking and converting to long
            {
                long readOffset = i * _blockSize;
                if (readOffset < 0)
                {
                    throw new Exception();
                }
                blocksQueue.PushBlock(new FileBlock(_blockSize, readOffset));
            }

            var size = fileInfo.Length - (blocksCount - 1) * _blockSize;
            var offset = (long)(blocksCount - 1) * _blockSize;
            blocksQueue.PushBlock(new FileBlock(size, offset));
            return blocksQueue;
        }

        protected override void Write(BlocksQueue writeQueue, string outputFile)
        {
            WriteHeader(writeQueue, outputFile);
            while (writeQueue.PreparedBlocks < writeQueue.TotalBlocksCount && !writeQueue.IsStoped)
            {
                try
                {
                    var block = writeQueue.PopBlock();
                    WriteBlock(outputFile, block);
                    writeQueue.PreparedBlocks++;
                }
                catch (Exception e)
                {
                    writeQueue.Stop();
                    _logger.Error(e, "An error occurred while writing block of compressed file");
                }
            }

            if (!writeQueue.IsStoped)
            {
                _logger.Info("File compressed");
            }
        }

        protected override void ReadBlock(string path, FileBlock block)
        {
            try
            {
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
                using (var stream = new MemoryStream())
                {
                    using (var gzip = new GZipStream(stream, CompressionMode.Compress))
                    {
                        fileStream.Position = block.ReadOffset;
                        var buffer = new byte[block.Size];
                        fileStream.Read(buffer, 0, (int)block.Size);
                        gzip.Write(buffer, 0, (int)block.Size);
                    }
                    block.Data = stream.ToArray();
                }
            }
            catch (Exception e)
            {
                block.HasError = true;
                _logger.Error(e, string.Format("An error occurred while preparing block (offset = {0}, size = {1}) of compressed file", block.ReadOffset, block.Size));
            }
        }

        private void WriteHeader(BlocksQueue writeQueue, string outputFile)
        {
            using (var outStream = new FileStream(outputFile, FileMode.OpenOrCreate, FileAccess.Write))
            using (var binaryWriter = new BinaryWriter(outStream))
            {
                binaryWriter.Write(writeQueue.TotalBlocksCount);
                _writeHeaderOffset += sizeof(int);
                _writeDataOffset += GetBlocksSize(writeQueue.TotalBlocksCount);
            }
        }

        private void WriteBlock(string file, FileBlock block)
        {
            using (var outStream = new FileStream(file, FileMode.OpenOrCreate, FileAccess.Write))
            using (var binaryWriter = new BinaryWriter(outStream))
            {
                outStream.Position = _writeHeaderOffset;
                binaryWriter.Write(block.Data.LongLength);
                binaryWriter.Write(block.ReadOffset);
                _writeHeaderOffset += sizeof(long) + sizeof(long);
                outStream.Position = _writeDataOffset;
                binaryWriter.Write(block.Data);
                _writeDataOffset += block.Data.Length;
                binaryWriter.Flush();
                block.ClearData();
            }
        }

        private long _writeDataOffset;
        private long _writeHeaderOffset;
    }
}
