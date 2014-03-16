using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class ChainStateBuilder
    {
        private readonly ChainedBlocksBuilder chainedBlocks;
        private readonly UtxoBuilder utxo;
        private readonly BuilderStats stats;

        public ChainStateBuilder(ChainedBlocksBuilder chainedBlocks, UtxoBuilder utxo)
        {
            this.chainedBlocks = chainedBlocks;
            this.utxo = utxo;
            this.stats = new BuilderStats();
            this.IsConsistent = true;
        }

        ~ChainStateBuilder()
        {
            this.Dispose();
        }

        public bool IsConsistent { get; set; }

        public ChainedBlocksBuilder ChainedBlocks { get { return this.chainedBlocks; } }

        public UtxoBuilder Utxo { get { return this.utxo; } }

        public BuilderStats Stats { get { return this.stats; } }

        public void Dispose()
        {
            this.utxo.Dispose();
            GC.SuppressFinalize(this);
        }

        public sealed class BuilderStats
        {
            public long totalTxCount;
            public long totalInputCount;
            public Stopwatch totalStopwatch = new Stopwatch();
            public Stopwatch currentRateStopwatch = new Stopwatch();
            public Stopwatch validateStopwatch = new Stopwatch();
            public long currentBlockCount;
            public long currentTxCount;
            public long currentInputCount;

            internal BuilderStats() { }
        }
    }
}
