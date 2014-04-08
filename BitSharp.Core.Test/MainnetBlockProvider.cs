using BitSharp.Common.ExtensionMethods;
using BitSharp.Core;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Core.Test
{
    public class MainnetBlockProvider
    {
        private readonly Dictionary<string, Block> blocks;

        public MainnetBlockProvider()
        {
            this.blocks = new Dictionary<string, Block>();

            var assembly = Assembly.GetExecutingAssembly();
            using (var stream = assembly.GetManifestResourceStream("BitSharp.Core.Test.Blocks.zip"))
            using (var zip = new ZipArchive(stream))
            {
                foreach (var entry in zip.Entries)
                {
                    using (var blockStream = entry.Open())
                    {
                        this.blocks.Add(entry.Name, DataEncoder.DecodeBlock(blockStream));
                    }
                }
            }
        }

        public Block GetBlock(int index)
        {
            var name = "{0}.blk".Format2(index);
            return this.blocks[name];
        }

        public Block GetBlock(string hash)
        {
            var name = "{0}.blk".Format2(hash);
            return this.blocks[name];
        }
    }
}
