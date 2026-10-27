/* =====================================================================
   Drops the PcPartsMarketplace database so schema.sql can rebuild it.
   DEVELOPMENT ONLY: all data is lost.
   Usage: sqlcmd -S localhost -E -C -i Database/reset.sql -i Database/schema.sql
   ===================================================================== */

USE master;
GO

IF DB_ID(N'PcPartsMarketplace') IS NOT NULL
BEGIN
    -- Close open connections (Visual Studio, SSMS, a running API) first.
    ALTER DATABASE PcPartsMarketplace SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE PcPartsMarketplace;
    PRINT 'PcPartsMarketplace dropped.';
END
GO
