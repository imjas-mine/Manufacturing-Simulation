CREATE DATABASE FogLampManufacturing;
use FogLampManufacturing
CREATE TABLE Configuration (
    ConfigDescription NVARCHAR(50) PRIMARY KEY,
    Value NVARCHAR(50) NOT NULL
);

INSERT INTO Configuration (ConfigDescription, Value) VALUES
('TimeScale', '2'),
('Harness', '55'),
('Reflector', '35'),
('Housing', '24'),
('Lens', '40'),
('Bulb', '60'),
('Bezel', '75'),
('BinMin', '5'),
('RefreshSpan', '5'),
('AssemblyStations', '3'),
('OrderAmount', '1000');


USE FogLampManufacturing;
GO


----------------CREATING TABLES-------------------------------
CREATE TABLE dbo.Part (
    PartID INT IDENTITY(1,1) PRIMARY KEY,
    PartName VARCHAR(50) NOT NULL UNIQUE,
    DefaultCapacity INT NOT NULL
);

CREATE TABLE dbo.Station (
    StationID INT IDENTITY(1,1) PRIMARY KEY,
    StationName VARCHAR(100) NOT NULL,
    WorkerID INT NULL, 
    LastUpdatedTime DATETIME NULL
);

CREATE TABLE dbo.Worker (
    WorkerID INT IDENTITY(1,1) PRIMARY KEY,
    WorkerName VARCHAR(100) NOT NULL,
    SkillLevel VARCHAR(20) NOT NULL,
    BaseAssemblyTimeSeconds INT NULL,
    DefectRate FLOAT NULL 
);

CREATE TABLE dbo.Bin (
    BinID INT IDENTITY(1,1) PRIMARY KEY,
    PartID INT NOT NULL REFERENCES dbo.Part(PartID),
    StationID INT NOT NULL REFERENCES dbo.Station(StationID),
    CurrentQuantity INT NOT NULL,
    LastReplenishedTime DATETIME NULL,
    CONSTRAINT UQ_Bin_StationPart UNIQUE (StationID, PartID)
);

CREATE TABLE dbo.[Order] (
    OrderID INT IDENTITY(1,1) PRIMARY KEY,
    OrderAmount INT NOT NULL,
    CompletedAmount INT NOT NULL DEFAULT 0,
    InProcessAmount INT NOT NULL DEFAULT 0,
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    Status VARCHAR(20) NOT NULL DEFAULT 'OPEN'
);

CREATE TABLE dbo.AssemblyLog (
    AssemblyID INT IDENTITY(1,1) PRIMARY KEY,
    StationID INT NOT NULL REFERENCES dbo.Station(StationID),
    WorkerID INT NOT NULL REFERENCES dbo.Worker(WorkerID),
    OrderID INT NULL REFERENCES dbo.[Order](OrderID),
    StartTime DATETIME NOT NULL,
    EndTime DATETIME NULL,
    Status VARCHAR(20) NOT NULL,
    IsDefective BIT NOT NULL DEFAULT 0
);

CREATE TABLE dbo.Notification (
    NotificationID INT IDENTITY(1,1) PRIMARY KEY,
    StationID INT NULL REFERENCES dbo.Station(StationID),
    BinID INT NULL REFERENCES dbo.Bin(BinID),
    CreatedAt DATETIME NOT NULL DEFAULT GETDATE(),
    ResolvedAt DATETIME NULL,
    ResolvedBy VARCHAR(100) NULL,
    Message VARCHAR(200) NULL,
    IsResolved BIT NOT NULL DEFAULT 0
);

--------------------CREATING FUNCTIONS----------------------------------------------

CREATE FUNCTION dbo.fn_GetConfigValue(@Name VARCHAR(100))
RETURNS VARCHAR(100)
AS
BEGIN
    DECLARE @val VARCHAR(100);
    SELECT @val = Value FROM dbo.Configuration WHERE ConfigDescription = @Name;
    RETURN @val;
END;
GO

DECLARE @stations INT = ISNULL(TRY_CAST(dbo.fn_GetConfigValue('AssemblyStations') AS INT), 3);
IF NOT EXISTS (SELECT 1 FROM dbo.Station)
BEGIN
    DECLARE @i INT = 1;
    WHILE @i <= @stations
    BEGIN
        INSERT INTO dbo.Station (StationName) VALUES ('Station ' + CAST(@i AS VARCHAR(10)));
        SET @i = @i + 1;
    END
END;
GO


IF NOT EXISTS (SELECT 1 FROM dbo.Part WHERE PartName = 'Harness')
BEGIN
    INSERT INTO dbo.Part (PartName, DefaultCapacity)
    VALUES
      ('Harness', 55),
      ('Reflector', 35),
      ('Housing', 24),
      ('Lens', 40),
      ('Bulb', 60),
      ('Bezel', 75);
END;
GO


IF NOT EXISTS (SELECT 1 FROM dbo.Bin)
BEGIN
    INSERT INTO dbo.Bin (PartID, StationID, CurrentQuantity, LastReplenishedTime)
    SELECT p.PartID, s.StationID, p.DefaultCapacity, GETDATE()
    FROM dbo.Part p CROSS JOIN dbo.Station s;
END;
GO


-------------------------TRIGGER---------------------------------------

CREATE OR ALTER TRIGGER trg_Bin_AfterUpdate
ON dbo.Bin
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH changed AS (
        SELECT i.BinID, i.StationID, i.CurrentQuantity
        FROM inserted i
        JOIN deleted d ON i.BinID = d.BinID
        WHERE i.CurrentQuantity <> d.CurrentQuantity
    )
    INSERT INTO dbo.Notification (StationID, BinID, Message)
    SELECT c.StationID, c.BinID,
           'Part running low at station: BinID=' + CAST(c.BinID AS VARCHAR(10)) 
             + ' CurrentQty=' + CAST(c.CurrentQuantity AS VARCHAR(10))
    FROM changed c
    WHERE c.CurrentQuantity <= ISNULL(TRY_CAST(dbo.fn_GetConfigValue('BinMin') AS INT), 5)
      -- only create notification if there is no unresolved notification already for that bin
      AND NOT EXISTS (
          SELECT 1 FROM dbo.Notification n WHERE n.BinID = c.BinID AND n.IsResolved = 0
      );
END;
GO


--stored procdeure to decrement the quantitiy from a bin.
CREATE OR ALTER PROCEDURE dbo.usp_DecrementBin
    @BinID INT,
    @Qty INT = 1,
    @Success BIT OUTPUT,
    @NewQuantity INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Success = 0;
    SET @NewQuantity = NULL;

    -- 1. Check if we are already inside a transaction (Nested)
    DECLARE @Trancount INT = @@TRANCOUNT;
    DECLARE @SavepointName VARCHAR(32) = 'SavePoint_Decrement';

    BEGIN TRY
        -- 2. Start Transaction or Create Savepoint
        IF @Trancount > 0
            SAVE TRANSACTION @SavepointName; -- Just mark a spot to go back to
        ELSE
            BEGIN TRANSACTION; -- Start a fresh transaction

        -- 3. Check Logic
        DECLARE @current INT;
        SELECT @current = CurrentQuantity
        FROM dbo.Bin WITH (ROWLOCK, UPDLOCK) 
        WHERE BinID = @BinID;

        -- Bin Doesn't Exist
        IF @current IS NULL
        BEGIN
            IF @Trancount > 0 ROLLBACK TRANSACTION @SavepointName; 
            ELSE ROLLBACK TRANSACTION;
            RETURN;
        END

        -- Not Enough Parts
        IF @current < @Qty
        BEGIN
            -- Revert ONLY this specific step, don't kill the outer transaction
            IF @Trancount > 0
                ROLLBACK TRANSACTION @SavepointName;
            ELSE
                ROLLBACK TRANSACTION;
            
            SET @Success = 0;
            SET @NewQuantity = @current;
            RETURN;
        END

        -- 4. Update
        UPDATE dbo.Bin
        SET CurrentQuantity = CurrentQuantity - @Qty
        WHERE BinID = @BinID;

        SELECT @NewQuantity = CurrentQuantity FROM dbo.Bin WHERE BinID = @BinID;

        -- 5. Commit (Only if WE started the transaction)
        -- If we are nested, we do nothing. The outer proc will commit later.
        IF @Trancount = 0
            COMMIT TRANSACTION;

        SET @Success = 1;
    END TRY
    BEGIN CATCH
        -- Error Handling: Same Safe Rollback Logic
        IF XACT_STATE() <> 0
        BEGIN
            IF @Trancount > 0
                ROLLBACK TRANSACTION @SavepointName;
            ELSE
                ROLLBACK TRANSACTION;
        END
        
        SET @Success = 0;
        SET @NewQuantity = NULL;
        THROW; 
    END CATCH
END;
GO


--stored procedure to complete an assembly
CREATE OR ALTER PROCEDURE dbo.usp_CompleteAssembly
    @AssemblyID INT,
    @Success BIT OUTPUT,
    @Message VARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Success = 0;
    SET @Message = NULL;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @workerID INT, @orderID INT, @startTime DATETIME;
        SELECT @workerID = WorkerID, @orderID = OrderID, @startTime = StartTime
        FROM dbo.AssemblyLog WITH (UPDLOCK, ROWLOCK)
        WHERE AssemblyID = @AssemblyID;

        IF @workerID IS NULL
        BEGIN
            ROLLBACK TRAN;
            SET @Message = 'Assembly row not found';
            RETURN;
        END

        -- Get defect rate for worker; fall back to skill level defaults
        DECLARE @defectRate FLOAT;
        SELECT @defectRate = DefectRate FROM dbo.Worker WHERE WorkerID = @workerID;

        IF @defectRate IS NULL
        BEGIN
            -- fallback by skill level
            DECLARE @skill VARCHAR(20);
            SELECT @skill = SkillLevel FROM dbo.Worker WHERE WorkerID = @workerID;
            SET @defectRate = 
                CASE WHEN @skill = 'Rookie' THEN 0.0085
                     WHEN @skill = 'Super' THEN 0.0015
                     ELSE 0.005 END;
        END

        DECLARE @r FLOAT = ABS(CAST(CHECKSUM(NEWID()) AS int) % 1000000) / 1000000.0;
        DECLARE @isDefective BIT = CASE WHEN @r < @defectRate THEN 1 ELSE 0 END;

        -- Update AssemblyLog
        UPDATE dbo.AssemblyLog
        SET EndTime = GETDATE(),
            Status = CASE WHEN @isDefective = 1 THEN 'FAILED' ELSE 'COMPLETED' END,
            IsDefective = @isDefective
        WHERE AssemblyID = @AssemblyID;

        -- Update the order counts if applicable
        IF @orderID IS NOT NULL
        BEGIN
            -- decrement in-process
            UPDATE dbo.[Order]
            SET InProcessAmount = CASE WHEN InProcessAmount > 0 THEN InProcessAmount - 1 ELSE 0 END
            WHERE OrderID = @orderID;

            IF @isDefective = 0
            BEGIN
                UPDATE dbo.[Order]
                SET CompletedAmount = CompletedAmount + 1
                WHERE OrderID = @orderID;
            END
        END

        COMMIT TRAN;
        SET @Success = 1;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        SET @Success = 0;
        SET @Message = ERROR_MESSAGE();
        THROW;
    END CATCH
END;
GO


-- stored procedure to replenish a bin
CREATE OR ALTER PROCEDURE dbo.usp_ReplenishBin
    @BinID INT,
    @RunnerName VARCHAR(100) = NULL,
    @NewQuantity INT OUTPUT,
    @Success BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Success = 0;
    SET @NewQuantity = NULL;

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @partID INT, @oldQty INT, @defaultCapacity INT;
        SELECT @partID = PartID, @oldQty = CurrentQuantity FROM dbo.Bin WHERE BinID = @BinID;

        IF @partID IS NULL
        BEGIN
            ROLLBACK TRAN; RETURN;
        END

        SELECT @defaultCapacity = DefaultCapacity FROM dbo.Part WHERE PartID = @partID;
        IF @defaultCapacity IS NULL SET @defaultCapacity = 0;

        -- New quantity = default capacity + old remaining placed on top
        DECLARE @combined INT = @defaultCapacity + ISNULL(@oldQty,0);

        UPDATE dbo.Bin
        SET CurrentQuantity = @combined,
            LastReplenishedTime = GETDATE()
        WHERE BinID = @BinID;

        -- resolve any open notifications for this bin
        UPDATE dbo.Notification
        SET IsResolved = 1, ResolvedAt = GETDATE(), ResolvedBy = @RunnerName
        WHERE BinID = @BinID AND IsResolved = 0;

        SELECT @NewQuantity = CurrentQuantity FROM dbo.Bin WHERE BinID = @BinID;

        COMMIT TRAN;
        SET @Success = 1;
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        SET @Success = 0;
        THROW;
    END CATCH
END;
GO


---starting assembly
CREATE OR ALTER PROCEDURE dbo.usp_StartAssembly
    @StationID INT,
    @WorkerID INT,
    @OrderID INT = NULL,
    @AssemblyID INT OUTPUT,
    @Success BIT OUTPUT,
    @Message VARCHAR(200) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @Success = 0;
    SET @AssemblyID = NULL;
    SET @Message = NULL;

    BEGIN TRY
        BEGIN TRAN;

        -- Get all bins for this station
        DECLARE @HarnessBinID INT, @ReflectorBinID INT, @HousingBinID INT, 
                @LensBinID INT, @BulbBinID INT, @BezelBinID INT;
        
        SELECT @HarnessBinID = BinID FROM dbo.Bin b 
        JOIN dbo.Part p ON b.PartID = p.PartID 
        WHERE b.StationID = @StationID AND p.PartName = 'Harness';
        
        SELECT @ReflectorBinID = BinID FROM dbo.Bin b 
        JOIN dbo.Part p ON b.PartID = p.PartID 
        WHERE b.StationID = @StationID AND p.PartName = 'Reflector';
        
        SELECT @HousingBinID = BinID FROM dbo.Bin b 
        JOIN dbo.Part p ON b.PartID = p.PartID 
        WHERE b.StationID = @StationID AND p.PartName = 'Housing';
        
        SELECT @LensBinID = BinID FROM dbo.Bin b 
        JOIN dbo.Part p ON b.PartID = p.PartID 
        WHERE b.StationID = @StationID AND p.PartName = 'Lens';
        
        SELECT @BulbBinID = BinID FROM dbo.Bin b 
        JOIN dbo.Part p ON b.PartID = p.PartID 
        WHERE b.StationID = @StationID AND p.PartName = 'Bulb';
        
        SELECT @BezelBinID = BinID FROM dbo.Bin b 
        JOIN dbo.Part p ON b.PartID = p.PartID 
        WHERE b.StationID = @StationID AND p.PartName = 'Bezel';

        -- Try to decrement all bins
        DECLARE @binSuccess BIT, @newQty INT;
        DECLARE @allSuccess BIT = 1;
        DECLARE @failedPart VARCHAR(50) = NULL;

        -- Decrement each part
        EXEC dbo.usp_DecrementBin @HarnessBinID, 1, @binSuccess OUTPUT, @newQty OUTPUT;
        IF @binSuccess = 0 BEGIN SET @allSuccess = 0; SET @failedPart = 'Harness'; END

        IF @allSuccess = 1
        BEGIN
            EXEC dbo.usp_DecrementBin @ReflectorBinID, 1, @binSuccess OUTPUT, @newQty OUTPUT;
            IF @binSuccess = 0 BEGIN SET @allSuccess = 0; SET @failedPart = 'Reflector'; END
        END

        IF @allSuccess = 1
        BEGIN
            EXEC dbo.usp_DecrementBin @HousingBinID, 1, @binSuccess OUTPUT, @newQty OUTPUT;
            IF @binSuccess = 0 BEGIN SET @allSuccess = 0; SET @failedPart = 'Housing'; END
        END

        IF @allSuccess = 1
        BEGIN
            EXEC dbo.usp_DecrementBin @LensBinID, 1, @binSuccess OUTPUT, @newQty OUTPUT;
            IF @binSuccess = 0 BEGIN SET @allSuccess = 0; SET @failedPart = 'Lens'; END
        END

        IF @allSuccess = 1
        BEGIN
            EXEC dbo.usp_DecrementBin @BulbBinID, 1, @binSuccess OUTPUT, @newQty OUTPUT;
            IF @binSuccess = 0 BEGIN SET @allSuccess = 0; SET @failedPart = 'Bulb'; END
        END

        IF @allSuccess = 1
        BEGIN
            EXEC dbo.usp_DecrementBin @BezelBinID, 1, @binSuccess OUTPUT, @newQty OUTPUT;
            IF @binSuccess = 0 BEGIN SET @allSuccess = 0; SET @failedPart = 'Bezel'; END
        END

        IF @allSuccess = 0
        BEGIN
            ROLLBACK TRAN;
            SET @Message = 'Insufficient parts: ' + ISNULL(@failedPart, 'Unknown');
            RETURN;
        END

        -- Create assembly log entry
        INSERT INTO dbo.AssemblyLog (StationID, WorkerID, OrderID, StartTime, Status)
        VALUES (@StationID, @WorkerID, @OrderID, GETDATE(), 'IN_PROGRESS');

        SET @AssemblyID = SCOPE_IDENTITY();

        -- Update order in-process count
        IF @OrderID IS NOT NULL
        BEGIN
            UPDATE dbo.[Order]
            SET InProcessAmount = InProcessAmount + 1
            WHERE OrderID = @OrderID;
        END

        COMMIT TRAN;
        SET @Success = 1;
        SET @Message = 'Assembly started successfully';
    END TRY
    BEGIN CATCH
        IF XACT_STATE() <> 0 ROLLBACK TRAN;
        SET @Success = 0;
        SET @Message = ERROR_MESSAGE();
        THROW;
    END CATCH
END;
GO
------------------------------------CREATING VIEWS------------------------------------------------------

-- Bin status
CREATE OR ALTER VIEW dbo.vw_BinStatus AS
SELECT 
    b.BinID,
    b.StationID,
    s.StationName,
    b.PartID,
    p.PartName,
    b.CurrentQuantity,
    p.DefaultCapacity,
    b.LastReplenishedTime,
    CASE WHEN b.CurrentQuantity <= ISNULL(TRY_CAST(dbo.fn_GetConfigValue('BinMin') AS INT),5) THEN 1 ELSE 0 END AS IsLow
FROM dbo.Bin b
JOIN dbo.Part p ON b.PartID = p.PartID
JOIN dbo.Station s ON b.StationID = s.StationID;
GO

-- Station overview for Andon (per station): in-progress assemblies, produced count, low bins count
CREATE OR ALTER VIEW dbo.vw_StationStatus AS
SELECT
    s.StationID,
    s.StationName,
    ISNULL(inprog.InProgressCount,0) AS InProgress,
    ISNULL(prod.ProducedCount,0) AS Produced,
    ISNULL(low.LowBins,0) AS LowBins
FROM dbo.Station s
LEFT JOIN (
    SELECT StationID, COUNT(*) AS InProgressCount
    FROM dbo.AssemblyLog
    WHERE Status = 'IN_PROGRESS'
    GROUP BY StationID
) inprog ON s.StationID = inprog.StationID
LEFT JOIN (
    SELECT StationID, COUNT(*) AS ProducedCount
    FROM dbo.AssemblyLog
    WHERE Status = 'COMPLETED'
    GROUP BY StationID
) prod ON s.StationID = prod.StationID
LEFT JOIN (
    SELECT StationID, COUNT(*) AS LowBins
    FROM dbo.Bin
    WHERE CurrentQuantity <= ISNULL(TRY_CAST(dbo.fn_GetConfigValue('BinMin') AS INT),5)
    GROUP BY StationID
) low ON s.StationID = low.StationID;
GO

-- Kanban overview (line-level)
CREATE OR ALTER VIEW dbo.vw_KanbanOverview AS
SELECT
    o.OrderID,
    o.OrderAmount,
    o.InProcessAmount,
    o.CompletedAmount,
    CAST(CASE WHEN o.OrderAmount > 0 THEN (CAST(o.CompletedAmount AS FLOAT) / o.OrderAmount) * 100.0 ELSE NULL END AS DECIMAL(5,2)) AS PercentComplete,
    CAST(CASE WHEN (o.CompletedAmount + NULLIF( (SELECT SUM(CASE WHEN IsDefective = 1 THEN 1 ELSE 0 END) FROM dbo.AssemblyLog WHERE OrderID = o.OrderID),0)) > 0
              THEN (CAST(o.CompletedAmount AS FLOAT) / NULLIF( (o.CompletedAmount + (SELECT SUM(CASE WHEN IsDefective = 1 THEN 1 ELSE 0 END) FROM dbo.AssemblyLog WHERE OrderID = o.OrderID) ),0) ) * 100.0
              ELSE NULL END AS DECIMAL(5,2)) AS YieldPercent
FROM dbo.[Order] o;
GO


CREATE OR ALTER VIEW dbo.vw_WorkerInfo AS
SELECT 
    w.WorkerID,
    w.WorkerName,
    w.SkillLevel,
    ISNULL(w.BaseAssemblyTimeSeconds, 
        CASE w.SkillLevel
            WHEN 'Rookie' THEN CAST(dbo.fn_GetConfigValue('RookieAssemblyTimeSeconds') AS INT)
            WHEN 'Super' THEN CAST(dbo.fn_GetConfigValue('SuperAssemblyTimeSeconds') AS INT)
            ELSE CAST(dbo.fn_GetConfigValue('ExperiencedAssemblyTimeSeconds') AS INT)
        END
    ) AS AssemblyTimeSeconds,
    ISNULL(w.DefectRate,
        CASE w.SkillLevel
            WHEN 'Rookie' THEN CAST(dbo.fn_GetConfigValue('RookieDefectRate') AS FLOAT)
            WHEN 'Super' THEN CAST(dbo.fn_GetConfigValue('SuperDefectRate') AS FLOAT)
            ELSE CAST(dbo.fn_GetConfigValue('ExperiencedDefectRate') AS FLOAT)
        END
    ) AS DefectRate
FROM dbo.Worker w;
GO

