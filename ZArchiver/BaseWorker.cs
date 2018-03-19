using System;
using System.IO;
using System.Threading;

namespace ZArchiver
{
    /// <summary>
    /// Базовый класс для компрессора и декопрессора
    /// </summary>
    public abstract class BaseWorker
    {
        public BaseWorker(int blockSize, int maxThreadCount)
        {
            _blockSize = blockSize;
            _maxThreadCount = maxThreadCount;
        }

        /// <summary>
        /// Очередь блочной обработки файлов
        /// </summary>
        protected void DoWork(string filename, string outputFile)
        {
            var blockInfo = GetBlockInfo(filename);
            var queueState = new QueueState(0, 0, _maxThreadCount);
            while (queueState.CurrentBlockIndex < blockInfo.Length)
            {
                if (queueState.FreeThreads > 0)
                {
                    queueState.FreeThreads--;
                    var block = blockInfo[queueState.CurrentBlockIndex];
                    ThreadStart starter = () => {  queueState.HasError = !PrepareBlock(filename, block); };
                    starter += () =>
                    {
                        queueState.PreparedBlocks++;
                        queueState.FreeThreads++;
                        if (queueState.PreparedBlocks == blockInfo.Length)
                        {
                            if (!queueState.HasError)
                            {
                                CombineBlocks(outputFile, blockInfo);
                            }
                            else
                            {
                                CleanBlocks(blockInfo);
                            }
                        }
                    };
                    var thread = new Thread(starter);
                    thread.Start();
                    queueState.CurrentBlockIndex++;
                }
            }
        }

        /// <summary>
        /// Получает информацию о блоках из файла
        /// </summary>
        /// <param name="path">Путь до файла</param>
        protected abstract BlockInfo[] GetBlockInfo(string path);

        /// <summary>
        /// Обработка блока файла
        /// </summary>
        /// <param name="path">Путь к исходному файлу</param>
        /// <param name="block">Обрабатываемый блок</param>
        /// <returns>Статус обработки блока</returns>
        protected abstract Boolean PrepareBlock(string path, BlockInfo block);

        /// <summary>
        /// Объединяет блоки в выходной файл
        /// </summary>
        /// <param name="output">Путь к выходому файлу</param>
        /// <param name="blockInfo">Информация о блоках</param>
        protected abstract void CombineBlocks(string output, BlockInfo[] blockInfo);

        /// <summary>
        /// Очищает мусор в случае ошибки
        /// </summary>
        /// <param name="blockInfo"></param>
        protected void CleanBlocks(BlockInfo[] blockInfo)
        {
            foreach(var block in blockInfo)
            {
                if (File.Exists(block.FileName))
                {
                    File.Delete(block.FileName);
                }
            }
        }

        /// <summary>
        /// Очищает выходной файл
        /// </summary>
        /// <param name="path">Путь к выходному файлу</param>
        protected void CleanOutputFile(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// Возвращает название части имени файла
        /// </summary>
        protected String GetPartName(string path, int ind)
        {
            return path + "." + ind;
        }

        protected readonly int _blockSize;
        protected readonly int _maxThreadCount;
    }
}
