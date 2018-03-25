using NLog;
using System;
using System.IO;
using System.IO.Compression;
using System.Threading;

namespace ZArchiver
{
    //public class Decompressor : BaseWorker
    //{
    //    public Decompressor(int blockSize, int maxThreadCount) : base(blockSize, maxThreadCount)
    //    {
    //        _logger = LogManager.GetCurrentClassLogger();
    //    }

    //    public void Decompress(string filename, string outputFile)
    //    {
    //        _logger.Info("Start decompressing file {0} to {1}", filename, outputFile);
    //        try
    //        {
    //            DoWork(filename, outputFile);
    //        }
    //        catch (Exception e)
    //        {
    //            _logger.Error(e, string.Format("An error occurred while decompressing the file {0}", filename));
    //        }
    //    }

    //    protected override BlockInfo[] GetBlockInfo(string path)
    //    {
    //        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read))
    //        using (var reader = new BinaryReader(stream))
    //        {
    //            var length = reader.ReadInt32();
    //            var blocksInfo = new BlockInfo[length];
    //            var offset = (length + 1) * sizeof(Int32);
    //            for (int i = 0; i < length; ++i)
    //            {
    //                var blockSize = reader.ReadInt32();
    //                blocksInfo[i] = new BlockInfo(GetPartName(path, i), blockSize, offset);
    //                offset += blockSize;
    //            }
    //            return blocksInfo;
    //        }
    //    }

    //    protected override Boolean PrepareBlock(string path, BlockInfo block)
    //    {
    //        try
    //        {
    //            using (var compressedFileStream = new FileStream(path, FileMode.Open, FileAccess.Read))
    //            using (var gzip = new GZipStream(compressedFileStream, CompressionMode.Decompress))
    //            using (var fileStream = new FileStream(block.FileName, FileMode.OpenOrCreate, FileAccess.Write))
    //            {
    //                compressedFileStream.Position = block.Offset;
    //                gzip.CopyTo(fileStream);
    //                fileStream.Flush();
    //            }
    //            return true;
    //        }
    //        catch (Exception e)
    //        {
    //            _logger.Error(e, string.Format("An error occurred while preparing block (offset = {0}, size = {1}, filename = {2}) of compressed file", block.Offset, block.Size, block.FileName));
    //            return false;
    //        }
    //    }

    //    protected override void CombineBlocks(string output, BlockInfo[] blockInfo)
    //    {
    //        try
    //        {
    //            using (var outputStream = new FileStream(output, FileMode.OpenOrCreate))
    //            using (var writer = new BinaryWriter(outputStream))
    //            {
    //                for (int i = 0; i < blockInfo.Length; ++i)
    //                {
    //                    var buff = File.ReadAllBytes(blockInfo[i].FileName);
    //                    File.Delete(blockInfo[i].FileName);
    //                    writer.Write(buff);
    //                    writer.Flush();
    //                }
    //            }
    //            _logger.Info("Decompressing file completed successfully");
    //        }
    //        catch (Exception e)
    //        {
    //            CleanBlocks(blockInfo);
    //            CleanOutputFile(output);
    //            _logger.Error(e, "An error occurred while combining parts of decompressed file");
    //        }
    //    }

    //    private readonly ILogger _logger;
    //}

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
                    _logger.Error(e, "An error occurred while writing block of compressed file");
                }
            }

            _logger.Info(string.Format("File compressed"));
        }

        private void ReadBlock(string filename, FileBlock block)
        {
            try
            {
                using (var fileStream = new FileStream(filename, FileMode.Open, FileAccess.Read))
                using (var stream = new MemoryStream())
                using (var gzip = new GZipStream(fileStream, CompressionMode.Decompress))
                {
                    fileStream.Position = block.ReadOffset + 1;
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
