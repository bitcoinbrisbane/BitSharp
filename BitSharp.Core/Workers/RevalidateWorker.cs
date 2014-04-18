using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using BitSharp.Core.Storage;
using NLog;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Workers
{
    //TODO
    public class RevalidateWorker : Worker
    {
        private readonly Logger logger;
        private readonly BlockHeaderCache blockHeaderCache;

        public RevalidateWorker(Logger logger, BlockHeaderCache blockHeaderCache)
            : base("RevalidateWorker", initialNotify: false, minIdleTime: TimeSpan.Zero, maxIdleTime: TimeSpan.MaxValue, logger: logger)
        {
            this.logger = logger;
            this.blockHeaderCache = blockHeaderCache;
        }

        protected override void WorkAction()
        {
            //TODO
            Chain blockchain = null;
            Block genesisBlock = null;
            throw new NotImplementedException();

            var stopwatch = new Stopwatch();
            stopwatch.Start();
            try
            {
                //TODO delete corrupted data? could get stuck in a fail-loop on the winning chain otherwise

                // verify blockchain has blocks
                if (blockchain.Blocks.Count == 0)
                    throw new ValidationException(0);

                // verify genesis block hash
                if (blockchain.Blocks[0].Hash != genesisBlock.Hash)
                    throw new ValidationException(blockchain.Blocks[0].Hash);

                // get genesis block header
                var chainGenesisBlockHeader = this.blockHeaderCache[blockchain.Blocks[0].Hash];

                // verify genesis block header
                if (
                    genesisBlock.Header.Version != chainGenesisBlockHeader.Version
                    || genesisBlock.Header.PreviousBlock != chainGenesisBlockHeader.PreviousBlock
                    || genesisBlock.Header.MerkleRoot != chainGenesisBlockHeader.MerkleRoot
                    || genesisBlock.Header.Time != chainGenesisBlockHeader.Time
                    || genesisBlock.Header.Bits != chainGenesisBlockHeader.Bits
                    || genesisBlock.Header.Nonce != chainGenesisBlockHeader.Nonce
                    || genesisBlock.Hash != chainGenesisBlockHeader.Hash
                    || genesisBlock.Hash != DataCalculator.CalculateBlockHash(chainGenesisBlockHeader))
                {
                    throw new ValidationException(chainGenesisBlockHeader.Hash);
                }

                // setup expected previous block hash value to verify each chain actually does link
                var expectedPreviousBlockHash = genesisBlock.Header.PreviousBlock;
                for (var height = 0; height < blockchain.Blocks.Count; height++)
                {
                    // cooperative loop
                    if (!this.IsStarted)
                        break;

                    // get the current link in the chain
                    var chainedHeader = blockchain.Blocks[height];

                    // verify height
                    if (chainedHeader.Height != height)
                        throw new ValidationException(chainedHeader.Hash);

                    // verify blockchain linking
                    if (chainedHeader.PreviousBlockHash != expectedPreviousBlockHash)
                        throw new ValidationException(chainedHeader.Hash);

                    // verify block exists
                    var blockHeader = this.blockHeaderCache[chainedHeader.Hash];

                    // verify block metadata matches header values
                    if (blockHeader.PreviousBlock != chainedHeader.PreviousBlockHash)
                        throw new ValidationException(chainedHeader.Hash);

                    // verify block header hash
                    if (DataCalculator.CalculateBlockHash(blockHeader) != chainedHeader.Hash)
                        throw new ValidationException(chainedHeader.Hash);

                    // next block metadata should have the current metadata's hash as its previous hash value
                    expectedPreviousBlockHash = chainedHeader.Hash;
                }

                // all validation passed
            }
            finally
            {
                stopwatch.Stop();
                this.logger.Info("Blockchain revalidation: {0:#,##0.000000}s".Format2(stopwatch.ElapsedSecondsFloat()));
            }
        }
    }
}
