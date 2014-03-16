using BitSharp.Common;
using BitSharp.Common.ExtensionMethods;
using BitSharp.Data;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace BitSharp.Storage
{
    public class ChainedBlockCache : BoundedCache<UInt256, ChainedBlock>
    {
        public event Action OnMaxTotalWorkBlocksChanged;

        private readonly CacheContext _cacheContext;

        private BigInteger maxTotalWork;
        private ImmutableList<UInt256> maxTotalWorkBlocks;
        private readonly ReaderWriterLockSlim maxTotalWorkLock;

        public ChainedBlockCache(CacheContext cacheContext)
            : base("ChainedBlockCache", cacheContext.StorageContext.ChainedBlockStorage)
        {
            this._cacheContext = cacheContext;

            this.maxTotalWork = -1;
            this.maxTotalWorkBlocks = ImmutableList.Create<UInt256>();
            this.maxTotalWorkLock = new ReaderWriterLockSlim();

            //TODO periodic rescan
            var checkThread = new Thread(() =>
            {
                new MethodTimer().Time("SelectMaxTotalWorkBlocks", () =>
                {
                    foreach (var keyPair in this._cacheContext.StorageContext.ChainedBlockStorage.SelectMaxTotalWorkBlocks())
                        CheckTotalWork(keyPair.Key, keyPair.Value);
                });

                //Debugger.Break();
            });
            checkThread.Start();
        }

        public CacheContext CacheContext { get { return this._cacheContext; } }

        public IStorageContext StorageContext { get { return this.CacheContext.StorageContext; } }

        public IImmutableList<UInt256> MaxTotalWorkBlocks
        {
            get
            {
                return this.maxTotalWorkLock.DoRead(() => this.maxTotalWorkBlocks.ToImmutableList());
            }
        }

        public override bool TryAdd(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            var result = base.TryAdd(blockHash, chainedBlock);
            CheckTotalWork(blockHash, chainedBlock);
            return result;
        }

        public override ChainedBlock this[UInt256 blockHash]
        {
            get
            {
                return base[blockHash];
            }
            set
            {
                base[blockHash] = value;
                CheckTotalWork(blockHash, value);
            }
        }

        private void CheckTotalWork(UInt256 blockHash, ChainedBlock chainedBlock)
        {
            this.maxTotalWorkLock.DoWrite(() =>
            {
                if (chainedBlock.TotalWork > this.maxTotalWork)
                {
                    this.maxTotalWork = chainedBlock.TotalWork;
                    this.maxTotalWorkBlocks = ImmutableList.Create<UInt256>(blockHash);

                    var handler = this.OnMaxTotalWorkBlocksChanged;
                    if (handler != null)
                        handler();
                }
                else if (chainedBlock.TotalWork == this.maxTotalWork)
                {
                    this.maxTotalWorkBlocks = this.maxTotalWorkBlocks.Add(blockHash);

                    var handler = this.OnMaxTotalWorkBlocksChanged;
                    if (handler != null)
                        handler();
                }
            });
        }
    }
}