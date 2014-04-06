using BitSharp.BlockHelper;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.BlockHelper
{
    public class BlockHelper
    {
        public static void Main(string[] args)
        {
            var projectFolder = Environment.CurrentDirectory;
            while (!projectFolder.EndsWith(@"\BitSharp.BlockHelper", StringComparison.InvariantCultureIgnoreCase))
                projectFolder = Path.GetDirectoryName(projectFolder);

            var blockFolder = Path.Combine(projectFolder, "Blocks");

            var blockExplorerProvider = new BlockExplorerProvider();

            var height = 0;
            foreach (var block in blockExplorerProvider.GetBlocks(Enumerable.Range(0, 10.THOUSAND())))
            {
                var heightFile = new FileInfo(Path.Combine(blockFolder, "{0}.blk".Format2(height)));
                height++;

                using (var stream = new FileStream(heightFile.FullName, FileMode.Create))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(DataEncoder.EncodeBlock(block));
                }

                var hashFile = new FileInfo(Path.Combine(blockFolder, "{0}.blk".Format2(block.Hash)));

                using (var stream = new FileStream(hashFile.FullName, FileMode.Create))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(DataEncoder.EncodeBlock(block));
                }
            }
        }
    }
}
