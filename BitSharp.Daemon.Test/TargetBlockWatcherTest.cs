using BitSharp.Data;
using BitSharp.Storage;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Daemon.Test
{
    [TestClass]
    public class TargetBlockWatcherTest
    {
        [TestMethod]
        public void TestSimpleTargetBlock()
        {
            // initialize data
            var chainedBlock0 = new ChainedBlock(blockHash: 0, previousBlockHash: 9999, height: 0, totalWork: 0);
            var chainedBlock1 = new ChainedBlock(blockHash: 1, previousBlockHash: chainedBlock0.BlockHash, height: 1, totalWork: 1);

            // initialize storage
            var memoryCacheContext = new CacheContext(new MemoryStorageContext());

            // initialize the target block watcher
            using (var targetBlockWatcher = new TargetBlockWatcher(memoryCacheContext))
            {
                // monitor event firing
                var onTargetBlockChangedCount = 0;
                targetBlockWatcher.OnTargetBlockChanged += () => onTargetBlockChangedCount++;

                // verify initial state
                Assert.IsNull(targetBlockWatcher.TargetBlock);

                // add block 0
                memoryCacheContext.ChainedBlockCache[chainedBlock0.BlockHash] = chainedBlock0;
                Assert.AreEqual(1, onTargetBlockChangedCount);

                // verify block 0
                Assert.AreEqual(chainedBlock0, targetBlockWatcher.TargetBlock);

                // add block 1
                memoryCacheContext.ChainedBlockCache[chainedBlock1.BlockHash] = chainedBlock1;
                Assert.AreEqual(2, onTargetBlockChangedCount);

                // verify block 1
                Assert.AreEqual(chainedBlock1, targetBlockWatcher.TargetBlock);
            }
        }
    }
}
