using BitSharp.Blockchain.ExtensionMethods;
using BitSharp.Common;
using BitSharp.Data;
using BitSharp.Storage;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Blockchain
{
    public class BlockchainWalker
    {
        public BlockchainPath GetBlockchainPath(ChainedBlock fromBlock, ChainedBlock toBlock, Func<UInt256, ChainedBlock> getChainedBlock, CancellationToken? cancelToken = null)
        {
            var rewindBlocks = ImmutableList.CreateBuilder<ChainedBlock>();
            var advanceBlocks = ImmutableList.CreateBuilder<ChainedBlock>();

            foreach (var pathElement in DiscoverPath(fromBlock, toBlock, getChainedBlock, cancelToken))
            {
                switch (pathElement.Chain)
                {
                    case PathChain.From:
                        rewindBlocks.Add(pathElement.Block);
                        break;

                    case PathChain.To:
                        advanceBlocks.Add(pathElement.Block);
                        break;

                    case PathChain.LastCommon:
                        advanceBlocks.Reverse();
                        return new BlockchainPath(fromBlock, toBlock, pathElement.Block, rewindBlocks.ToImmutable(), advanceBlocks.ToImmutable());

                    default:
                        throw new InvalidEnumArgumentException();
                }
            }

            throw new InvalidOperationException();
        }

        public ChainedBlock FindLastCommonBlock(ChainedBlock fromBlock, ChainedBlock toBlock, Func<UInt256, ChainedBlock> getChainedBlock, CancellationToken? cancelToken = null)
        {
            return DiscoverPath(fromBlock, toBlock, getChainedBlock, cancelToken)
                .Last().Block;
        }

        private IEnumerable<PathElement> DiscoverPath(ChainedBlock fromBlock, ChainedBlock toBlock, Func<UInt256, ChainedBlock> getChainedBlock, CancellationToken? cancelToken)
        {
            // find height difference between chains
            var heightDelta = toBlock.Height - fromBlock.Height;

            var currentFromBlock = fromBlock;
            var currentToBlock = toBlock;

            while (currentFromBlock.BlockHash != currentToBlock.BlockHash)
            {
                // cooperative loop
                cancelToken.GetValueOrDefault(CancellationToken.None).ThrowIfCancellationRequested();

                // if no further rollback is possible, chain mismatch
                if (currentFromBlock.Height == 0 || currentToBlock.Height == 0)
                    throw new InvalidOperationException();

                // from chain is longer, rewind it
                if (currentFromBlock.Height > currentToBlock.BlockHash)
                {
                    yield return new PathElement(PathChain.From, currentFromBlock);
                    currentFromBlock = getChainedBlock(currentFromBlock.PreviousBlockHash);
                }
                // to chain is longer, rewind it
                else if (currentToBlock.Height > currentFromBlock.Height)
                {
                    yield return new PathElement(PathChain.To, currentToBlock);
                    currentToBlock = getChainedBlock(currentToBlock.PreviousBlockHash);
                }
                // chains are same height, rewind both
                else
                {
                    yield return new PathElement(PathChain.From, currentFromBlock);
                    yield return new PathElement(PathChain.To, currentToBlock);

                    currentFromBlock = getChainedBlock(currentFromBlock.PreviousBlockHash);
                    currentToBlock = getChainedBlock(currentToBlock.PreviousBlockHash);
                }
            }

            // return last common block
            yield return new PathElement(PathChain.LastCommon, currentFromBlock);
        }

        private class PathElement
        {
            private readonly PathChain chain;
            private readonly ChainedBlock block;

            public PathElement(PathChain chain, ChainedBlock block)
            {
                this.chain = chain;
                this.block = block;
            }

            public PathChain Chain { get { return this.chain; } }

            public ChainedBlock Block { get { return this.block; } }
        }

        private enum PathChain
        {
            From,
            To,
            LastCommon
        }
    }
}
