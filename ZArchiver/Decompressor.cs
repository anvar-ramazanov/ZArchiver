using NLog;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace ZArchiver
{
    public class Decompressor : ZArchiver
    {
        public Decompressor(int blockSize, int maxThreadCount) : base(blockSize, maxThreadCount)
        {
            _logger = LogManager.GetCurrentClassLogger();
        }

        public override void Launch(string filename, string outputFile)
        {
            try
            {
                _logger.Info("Start decompressing file {0}", filename);
                Decompress(filename, outputFile);
            }
            catch (Exception e)
            {
                _logger.Error(e, string.Format("An error occurred while decompressing the file {0}", filename));
            }
        }

        private void Decompress(string filename, string outputFile)
        {
            var readQueue = GetReadQueue(filename);
            var writeQueue = new BlocksQueue(readQueue.TotalBlocksCount);

            var writerThread = new Thread(() =>
            {
                Write(writeQueue, outputFile);
            });
            writerThread.Start();

            var maxThreadForRead = _maxThreadCount - 1;
            var semaphore = new Semaphore(maxThreadForRead, maxThreadForRead);
            while (readQueue.PreparedBlocks < readQueue.TotalBlocksCount && !readQueue.IsStoped && semaphore.WaitOne())
            {
                var readThread = new Thread(() =>
                {
                    var block = readQueue.PopBlock();
                    ReadBlock(filename, block);
                    if (block.HasError)
                    {
                        readQueue.Stop();
                        writeQueue.Stop();
                    }
                    else
                    {
                        writeQueue.PushBlock(block);
                    }
                    semaphore.Release();
                });
                readThread.Start();
                readQueue.PreparedBlocks++;
            }
        }

        protected override BlocksQueue GetReadQueue(string path)
        {
            using (var file = new FileStream(path, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(file))
            {
                var blocksCount = reader.ReadInt32();
                var readQueue = new BlocksQueue(blocksCount);
                var readOffset = (blocksCount * 2 + 1) * sizeof(int);
                for (int i =0; i< blocksCount; ++i)
                {
                    var size = reader.ReadInt32();
                    var writeOffset = reader.ReadInt32();
                    readQueue.PushBlock(new FileBlock(size, readOffset, writeOffset));
                    readOffset += size;
                }
                return readQueue;
            }
        }

        protected override void Write(BlocksQueue writeQueue, string outputFile)
        {
            while (writeQueue.PreparedBlocks < writeQueue.TotalBlocksCount && !writeQueue.IsStoped)
            {
                try
                {
                    var block = writeQueue.PopBlock();
                    if (block == null)
                    {
                        break;
                    }
                    WriteBlock(outputFile, block);
                    writeQueue.PreparedBlocks++;
                }
                catch (Exception e)
                {
                    writeQueue.Stop();
                    _logger.Error(e, "An error occurred while writing block of decompressed file");
                }
            }

            _logger.Info(string.Format("File decompressed"));
        }

        private void ReadBlock(string filename, FileBlock block)
        {
            try
            {
                using (var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                using (var stream = new MemoryStream())
                using (var gzip = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    fileStream.Position = block.ReadOffset;
                    gzip.CopyTo(stream);
                    block.Data = stream.ToArray();
                }
            }
            catch (Exception e)
            {
                block.HasError = true;
                _logger.Error(e, string.Format("An error occurred while preparing block (offset = {0}, size = {1}) of decompressed file", block.ReadOffset, block.Size));
            }
        }

        private void WriteBlock(string file, FileBlock block)
        {
            using (var outStream = new FileStream(file, FileMode.OpenOrCreate, FileAccess.Write))
            using (var binaryWriter = new BinaryWriter(outStream))
            {
                outStream.Position = block.WriteOffset;
                outStream.Write(block.Data, 0, block.Data.Length);
            }
        }
    }
}
