using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Data
{
    public class BlockchainPathBuilder
    {
        private ChainedBlock fromBlock;
        private ChainedBlock toBlock;
        private ChainedBlock lastCommonBlock;
        private readonly ImmutableList<ChainedBlock>.Builder rewindBlocks;
        private readonly ImmutableList<ChainedBlock>.Builder advanceBlocks;

        private readonly ReaderWriterLockSlim rwLock;

        public BlockchainPathBuilder(BlockchainPath parentPath)
        {
            this.fromBlock = parentPath.FromBlock;
            this.toBlock = parentPath.ToBlock;
            this.lastCommonBlock = parentPath.LastCommonBlock;
            this.rewindBlocks = parentPath.RewindBlocks.ToBuilder();
            this.advanceBlocks = parentPath.AdvanceBlocks.ToBuilder();
            this.rwLock = new ReaderWriterLockSlim();
        }

        public ChainedBlock FromBlock
        {
            get
            {
                return this.rwLock.DoRead(() =>
                    this.fromBlock);
            }
        }

        public ChainedBlock ToBlock
        {
            get
            {
                return this.rwLock.DoRead(() =>
                    this.toBlock);
            }
        }

        public ChainedBlock LastCommonBlock
        {
            get
            {
                return this.rwLock.DoRead(() =>
                    this.lastCommonBlock);
            }
        }

        public ImmutableList<ChainedBlock> RewindBlocks
        {
            get
            {
                return this.rwLock.DoRead(() =>
                    this.rewindBlocks.ToImmutable());
            }
        }

        public ImmutableList<ChainedBlock> AdvanceBlocks
        {
            get
            {
                return this.rwLock.DoRead(() =>
                    this.advanceBlocks.ToImmutable());
            }
        }

        public BlockchainPath ToImmutable()
        {
            return this.rwLock.DoRead(() =>
                new BlockchainPath(this.fromBlock, this.toBlock, this.lastCommonBlock, this.rewindBlocks.ToImmutable(), this.advanceBlocks.ToImmutable()));
        }

        public Tuple<int, ChainedBlock> PeekFromBlock()
        {
            return this.rwLock.DoRead(() =>
            {
                int direction;
                ChainedBlock popBlock;
                if (this.rewindBlocks.Count > 0)
                {
                    direction = -1;
                    popBlock = this.fromBlock;
                }
                else if (this.advanceBlocks.Count > 0)
                {
                    direction = +1;
                    popBlock = this.advanceBlocks.First();
                }
                else
                    return null;

                return Tuple.Create(direction, popBlock);
            });
        }

        //TODO don't use tuple
        public Tuple<int, ChainedBlock> PopFromBlock()
        {
            return this.rwLock.DoWrite(() =>
            {
                int direction;
                ChainedBlock popBlock;
                if (this.rewindBlocks.Count > 0)
                {
                    direction = -1;
                    popBlock = this.fromBlock;
                    this.rewindBlocks.RemoveAt(this.rewindBlocks.Count - 1);

                    if (this.rewindBlocks.Count > 0)
                        this.fromBlock = this.rewindBlocks.Last();
                    else
                        this.fromBlock = this.lastCommonBlock;
                }
                else if (this.advanceBlocks.Count > 0)
                {
                    direction = +1;
                    popBlock = this.advanceBlocks.First();
                    this.advanceBlocks.RemoveAt(0);

                    if (this.advanceBlocks.Count > 0)
                        this.fromBlock = this.advanceBlocks.First();
                    else
                        this.fromBlock = this.toBlock;

                    this.lastCommonBlock = this.fromBlock;
                }
                else
                    return null;

                return Tuple.Create(direction, popBlock);
            });
        }

        public Tuple<int, ChainedBlock> PeekToBlock()
        {
            return this.rwLock.DoRead(() =>
            {
                int direction;
                ChainedBlock popBlock;
                if (this.advanceBlocks.Count > 0)
                {
                    direction = -1;
                    popBlock = this.toBlock;
                }
                else if (this.rewindBlocks.Count > 0)
                {
                    direction = +1;
                    popBlock = this.rewindBlocks.First();
                }
                else
                    return null;

                return Tuple.Create(direction, popBlock);
            });
        }

        public Tuple<int, ChainedBlock> PopToBlock()
        {
            return this.rwLock.DoWrite(() =>
            {
                int direction;
                ChainedBlock popBlock;
                if (this.advanceBlocks.Count > 0)
                {
                    direction = -1;
                    popBlock = this.toBlock;
                    this.advanceBlocks.RemoveAt(this.advanceBlocks.Count - 1);

                    if (this.advanceBlocks.Count > 0)
                        this.toBlock = this.advanceBlocks.Last();
                    else
                        this.toBlock = this.lastCommonBlock;
                }
                else if (this.rewindBlocks.Count > 0)
                {
                    direction = +1;
                    popBlock = this.rewindBlocks.First();
                    this.rewindBlocks.RemoveAt(0);

                    if (this.rewindBlocks.Count > 0)
                        this.toBlock = this.rewindBlocks.First();
                    else
                        this.toBlock = this.fromBlock;

                    this.lastCommonBlock = this.toBlock;
                }
                else
                    return null;

                return Tuple.Create(direction, popBlock);
            });
        }

        public void AddFromBlock(ChainedBlock block)
        {
            this.rwLock.DoWrite(() =>
            {
                if (block.PreviousBlockHash != this.fromBlock.BlockHash)
                    throw new InvalidOperationException();

                this.rewindBlocks.Add(block);
                this.fromBlock = block;
            });
        }

        public void RemoveFromBlock(ChainedBlock block)
        {
            this.rwLock.DoWrite(() =>
            {
                if (block != this.fromBlock
                    || this.rewindBlocks.Count == 0)
                    throw new InvalidOperationException();

                this.rewindBlocks.RemoveAt(this.rewindBlocks.Count - 1);
                this.fromBlock = block;
            });
        }

        public void AddToBlock(ChainedBlock block)
        {
            this.rwLock.DoWrite(() =>
            {
                if (block.PreviousBlockHash != this.toBlock.BlockHash)
                    throw new InvalidOperationException();

                this.advanceBlocks.Add(block);
                this.toBlock = block;
            });
        }

        public void RemoveToBlock(ChainedBlock block)
        {
            this.rwLock.DoWrite(() =>
            {
                if (block != this.toBlock
                    || this.advanceBlocks.Count == 0)
                    throw new InvalidOperationException();

                this.advanceBlocks.RemoveAt(this.advanceBlocks.Count - 1);
                this.toBlock = block;
            });
        }
    }
}
