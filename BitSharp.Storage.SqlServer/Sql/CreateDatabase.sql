USE BitSharp
GO

IF OBJECT_ID('BlockHeaders') IS NULL
CREATE TABLE BlockHeaders
(
	BlockHash BINARY(32) NOT NULL,
	HeaderBytes BINARY(80) NOT NULL,
	CONSTRAINT PK_Blocks PRIMARY KEY NONCLUSTERED
	(
		BlockHash
	)
);

IF OBJECT_ID('ChainedBlocks') IS NULL
CREATE TABLE ChainedBlocks
(
	BlockHash BINARY(32) NOT NULL,
	PreviousBlockHash BINARY(32) NOT NULL,
	Height INTEGER NOT NULL,
	TotalWork BINARY(64) NOT NULL,
	CONSTRAINT PK_ChainedBlocks PRIMARY KEY NONCLUSTERED
	(
		BlockHash
	)
);

IF OBJECT_ID('KnownAddresses') IS NULL
CREATE TABLE KnownAddresses
(
	IPAddress BINARY(16) NOT NULL,
	Port BINARY(2) NOT NULL,
	Services BINARY(8) NOT NULL,
	"Time" BINARY(4) NOT NULL,
	CONSTRAINT PK_KnownAddresses PRIMARY KEY
	(
		IPAddress,
		Port
	)
);

IF OBJECT_ID('BlockTransactions') IS NULL
CREATE TABLE BlockTransactions
(
	BlockHash BINARY(32) NOT NULL,
	TxHashesBytes VARBINARY(MAX) NOT NULL,
	CONSTRAINT PK_BlockTransactions PRIMARY KEY NONCLUSTERED
	(
		BlockHash
	)
);

IF OBJECT_ID('Transactions') IS NULL
CREATE TABLE Transactions
(
	TxHash BINARY(32) NOT NULL,
	TxBytes VARBINARY(MAX) NOT NULL,
	CONSTRAINT PK_Transactions PRIMARY KEY NONCLUSTERED
	(
		TxHash
	)
);
