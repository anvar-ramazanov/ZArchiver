using NLog;
using System;
using System.IO;

namespace ZArchiver
{
    class Program
    {

        static void Main(string[] args)
        {
            _logger = LogManager.GetCurrentClassLogger();

            var maxThreadCount = Environment.ProcessorCount * 2;
            var blockSize = 1024 * 1024; // 1 mb

            if (args.Length < 2)
            {
                _logger.Error("Wrong arguments count. Number of arguments must be at least two");
                return;
            }
            else if (args.Length > 3)
            {
                _logger.Error("Wrong arguments count. Number of arguments must be no more than three");
                return;
            }

            var action = ActionParser.Parse(args[0]);
            var filename = args[1];
            var output = args.Length == 2 ? string.Empty : args[2];


            if(action == Action.Unknown)
            {
                _logger.Error("Unknow first argument '{0}'. Action must be 'compress' or 'decompress'", args[0]);
                return;
            }

            if (!File.Exists(filename))
            {
                _logger.Error("File {0} does not exists", filename);
                return;
            }

            if (action == Action.Compress)
            {
                if(string.IsNullOrEmpty(output))
                {
                    output = filename + ".gz";
                }
                var compressor = new Compressor(blockSize, maxThreadCount);
                compressor.Launch(filename, output);
            }
            else
            {
                if (string.IsNullOrEmpty(output))
                {
                    output = filename + ".decompressed";
                }
                var decompressor = new Decompressor(blockSize, maxThreadCount);
                decompressor.Launch(filename, output);
            }
            Console.ReadKey();
        }

        private static ILogger _logger;
    }
}
