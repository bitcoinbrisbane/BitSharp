using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Core.Builders
{
    public class BlockchainPathBuilder
    {
        private ChainedHeader fromBlock;
        private ChainedHeader toBlock;
        private ChainedHeader lastCommonBlock;
        private readonly ImmutableList<ChainedHeader>.Builder rewindBlocks;
        private readonly ImmutableList<ChainedHeader>.Builder advanceBlocks;

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

        public ChainedHeader FromBlock
        {
            get
            {
                return this.rwLock.DoRead(() =>
                    this.fromBlock);
            }
        }

        public ChainedHeader ToBlock
        {
            get
            {
                return this.rwLock.DoRead(() =>
                    this.toBlock);
            }
        }

        public ChainedHeader LastCommonBlock
        {
            get
            {
                return this.rwLock.DoRead(() =>
                    this.lastCommonBlock);
            }
        }

        public ImmutableList<ChainedHeader> RewindBlocks
        {
            get
            {
                return this.rwLock.DoRead(() =>
                    this.rewindBlocks.ToImmutable());
            }
        }

        public ImmutableList<ChainedHeader> AdvanceBlocks
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

        public Tuple<int, ChainedHeader> PeekFromBlock()
        {
            return this.rwLock.DoRead(() =>
            {
                int direction;
                ChainedHeader popBlock;
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
        public Tuple<int, ChainedHeader> PopFromBlock()
        {
            return this.rwLock.DoWrite(() =>
            {
                int direction;
                ChainedHeader popBlock;
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

        public Tuple<int, ChainedHeader> PeekToBlock()
        {
            return this.rwLock.DoRead(() =>
            {
                int direction;
                ChainedHeader popBlock;
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

        public Tuple<int, ChainedHeader> PopToBlock()
        {
            return this.rwLock.DoWrite(() =>
            {
                int direction;
                ChainedHeader popBlock;
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

        public void AddFromBlock(ChainedHeader chainedHeader)
        {
            this.rwLock.DoWrite(() =>
            {
                if (chainedHeader.PreviousBlockHash != this.fromBlock.Hash)
                    throw new InvalidOperationException();

                this.rewindBlocks.Add(chainedHeader);
                this.fromBlock = chainedHeader;
            });
        }

        public void RemoveFromBlock(ChainedHeader chainedHeader)
        {
            this.rwLock.DoWrite(() =>
            {
                if (chainedHeader != this.fromBlock
                    || this.rewindBlocks.Count == 0)
                    throw new InvalidOperationException();

                this.rewindBlocks.RemoveAt(this.rewindBlocks.Count - 1);
                this.fromBlock = chainedHeader;
            });
        }

        public void AddToBlock(ChainedHeader chainedHeader)
        {
            this.rwLock.DoWrite(() =>
            {
                if (chainedHeader.PreviousBlockHash != this.toBlock.Hash)
                    throw new InvalidOperationException();

                this.advanceBlocks.Add(chainedHeader);
                this.toBlock = chainedHeader;
            });
        }

        public void RemoveToBlock(ChainedHeader chainedHeader)
        {
            this.rwLock.DoWrite(() =>
            {
                if (chainedHeader != this.toBlock
                    || this.advanceBlocks.Count == 0)
                    throw new InvalidOperationException();

                this.advanceBlocks.RemoveAt(this.advanceBlocks.Count - 1);
                this.toBlock = chainedHeader;
            });
        }
    }
}
