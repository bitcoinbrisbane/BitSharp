using BitSharp.Core.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BitSharp.Core
{
    public interface IMerkleTreePruningCursor : IDisposable
    {
        void BeginTransaction();

        void CommitTransaction();

        bool TryMoveToIndex(int index, out MerkleTreeNode node);

        bool TryMoveLeft(out MerkleTreeNode node);

        bool TryMoveRight(out MerkleTreeNode node);

        void WriteNode(MerkleTreeNode node);

        void MoveLeft();

        void DeleteNodeToRight();
    }
}
