using BitSharp.Common;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core.Test
{
    public static class AssertMethods
    {
        public static void AssertDaemonAtBlock(int expectedHeight, UInt256 expectedBlockHash, CoreDaemon daemon)
        {
            Assert.AreEqual(expectedHeight, daemon.TargetBlock.Height);
            Assert.AreEqual(expectedHeight, daemon.TargetChain.Height);
            Assert.AreEqual(expectedBlockHash, daemon.TargetChain.LastBlock.Hash);
            Assert.AreEqual(expectedHeight, daemon.ChainState.Height);
            Assert.AreEqual(expectedBlockHash, daemon.ChainState.LastBlockHash);
        }
    }
}
