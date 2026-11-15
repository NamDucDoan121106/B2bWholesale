/* =====================================================================
   B2B Wholesale Management System - Database script (SQL Server 2016+)
   Creates database B2BWholesaleDB, all tables, constraints and seed data.
   Run in SSMS / Azure Data Studio (F5). Safe to re-run: drops and recreates.

   Integrity rules
   - Order.AgentID      -> Agent   : ON UPDATE CASCADE, ON DELETE NO ACTION
                                     (an agent with order history cannot be deleted)
   - OrderDetail.OrderID-> Order   : ON UPDATE CASCADE, ON DELETE CASCADE
                                     (deleting an order removes its lines)
   - OrderDetail.ItemID -> Item    : ON UPDATE CASCADE, ON DELETE NO ACTION
                                     (an item that was sold cannot be deleted)
   - Users.AgentID      -> Agent   : ON UPDATE CASCADE, ON DELETE CASCADE
                                     (deleting an agent removes its portal login)
   - CHECK constraints keep stock, price and quantity valid; stock can never
     go negative, which is the last line of defence for the order transaction.
   ===================================================================== */

USE master;
GO
IF DB_ID(N'B2BWholesaleDB') IS NOT NULL
BEGIN
    ALTER DATABASE B2BWholesaleDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE B2BWholesaleDB;
END
GO
CREATE DATABASE B2BWholesaleDB;
GO
USE B2BWholesaleDB;
GO

-- ===== TABLES =====
CREATE TABLE dbo.Item (
    ItemID        INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Item PRIMARY KEY,
    ItemName      NVARCHAR(150)     NOT NULL,
    Size          NVARCHAR(100)     NOT NULL,
    Price         DECIMAL(18,2)     NOT NULL,
    StockQuantity INT               NOT NULL CONSTRAINT DF_Item_Stock DEFAULT (0),
    CONSTRAINT CK_Item_Price CHECK (Price >= 0),
    CONSTRAINT CK_Item_Stock CHECK (StockQuantity >= 0)
);
CREATE UNIQUE INDEX IX_Item_Name_Size ON dbo.Item (ItemName, Size);

CREATE TABLE dbo.Agent (
    AgentID   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Agent PRIMARY KEY,
    AgentName NVARCHAR(150)     NOT NULL,
    Address   NVARCHAR(300)     NOT NULL,
    Phone     NVARCHAR(20)      NOT NULL
);

CREATE TABLE dbo.[Order] (
    OrderID     INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Order PRIMARY KEY,
    OrderDate   DATETIME2(0)      NOT NULL CONSTRAINT DF_Order_Date DEFAULT (SYSDATETIME()),
    AgentID     INT               NOT NULL,
    OrderStatus NVARCHAR(20)      NOT NULL CONSTRAINT DF_Order_Status DEFAULT (N'Pending'),
    CONSTRAINT FK_Order_Agent FOREIGN KEY (AgentID) REFERENCES dbo.Agent (AgentID)
        ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT CK_Order_Status CHECK (OrderStatus IN (N'Pending', N'Confirmed', N'Shipped', N'Delivered', N'Cancelled'))
);
CREATE INDEX IX_Order_AgentID ON dbo.[Order] (AgentID);

CREATE TABLE dbo.OrderDetail (
    ID         INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_OrderDetail PRIMARY KEY,
    OrderID    INT               NOT NULL,
    ItemID     INT               NOT NULL,
    Quantity   INT               NOT NULL,
    UnitAmount DECIMAL(18,2)     NOT NULL,   -- unit price captured when the order was placed
    CONSTRAINT FK_OrderDetail_Order FOREIGN KEY (OrderID) REFERENCES dbo.[Order] (OrderID)
        ON UPDATE CASCADE ON DELETE CASCADE,
    CONSTRAINT FK_OrderDetail_Item FOREIGN KEY (ItemID) REFERENCES dbo.Item (ItemID)
        ON UPDATE CASCADE ON DELETE NO ACTION,
    CONSTRAINT CK_OrderDetail_Qty CHECK (Quantity > 0),
    CONSTRAINT CK_OrderDetail_Amount CHECK (UnitAmount >= 0)
);
CREATE INDEX IX_OrderDetail_OrderID ON dbo.OrderDetail (OrderID);
CREATE INDEX IX_OrderDetail_ItemID ON dbo.OrderDetail (ItemID);

CREATE TABLE dbo.Users (
    UserID              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Users PRIMARY KEY,
    UserName            NVARCHAR(50)      NOT NULL,
    email               NVARCHAR(150)     NOT NULL,
    password            NVARCHAR(256)     NOT NULL,   -- PBKDF2-SHA256 hash, never plain text
    [lock]              BIT               NOT NULL CONSTRAINT DF_Users_Lock DEFAULT (0),
    Role                NVARCHAR(20)      NOT NULL,
    FailedLoginAttempts INT               NOT NULL CONSTRAINT DF_Users_Failed DEFAULT (0),  -- auto-lock after 5
    AgentID             INT               NULL,       -- links an Agent-role login to its distributor
    CONSTRAINT UQ_Users_UserName UNIQUE (UserName),
    CONSTRAINT UQ_Users_Email UNIQUE (email),
    CONSTRAINT CK_Users_Role CHECK (Role IN (N'Admin', N'Agent')),
    CONSTRAINT CK_Users_AgentLink CHECK ((Role = N'Agent' AND AgentID IS NOT NULL) OR (Role = N'Admin' AND AgentID IS NULL)),
    CONSTRAINT FK_Users_Agent FOREIGN KEY (AgentID) REFERENCES dbo.Agent (AgentID)
        ON UPDATE CASCADE ON DELETE CASCADE
);
CREATE INDEX IX_Users_AgentID ON dbo.Users (AgentID);
GO

-- ===== SEED DATA (15-30 rows per table) =====
SET IDENTITY_INSERT dbo.Item ON;
INSERT INTO dbo.Item (ItemID, ItemName, Size, Price, StockQuantity) VALUES
(1, N'Vinamilk Fresh Milk 100%', N'Carton 48 x 180ml', 365000.00, 420),
(2, N'TH True Milk Less Sugar', N'Carton 48 x 180ml', 389000.00, 260),
(3, N'Nescafe 3in1 Instant Coffee', N'Case 24 bags x 20 sachets', 1150000.00, 85),
(4, N'Trung Nguyen G7 Coffee', N'Case 24 boxes x 21 sachets', 1320000.00, 12),
(5, N'Lavie Mineral Water', N'Pack 24 x 500ml', 98000.00, 1200),
(6, N'Aquafina Purified Water', N'Pack 24 x 500ml', 105000.00, 950),
(7, N'Coca-Cola Original', N'Carton 24 x 330ml can', 215000.00, 640),
(8, N'Pepsi Cola', N'Carton 24 x 330ml can', 205000.00, 18),
(9, N'Tiger Beer', N'Carton 24 x 330ml can', 395000.00, 310),
(10, N'Heineken Beer', N'Carton 24 x 330ml can', 485000.00, 9),
(11, N'Hao Hao Instant Noodles', N'Carton 30 packs x 75g', 118000.00, 780),
(12, N'Omachi Potato Noodles', N'Carton 30 packs x 80g', 245000.00, 340),
(13, N'ST25 Fragrant Rice', N'Bag 25kg', 875000.00, 150),
(14, N'Neptune Cooking Oil', N'Case 12 x 1L', 610000.00, 14),
(15, N'Chin-su Fish Sauce', N'Case 12 x 500ml', 395000.00, 220),
(16, N'Bien Hoa Refined Sugar', N'Bag 50kg', 1050000.00, 60),
(17, N'OMO Laundry Powder', N'Case 6 x 4.1kg', 1290000.00, 95),
(18, N'Sunlight Dishwashing Liquid', N'Case 12 x 750g', 342000.00, 7),
(19, N'Kinh Do Cosy Biscuits', N'Carton 12 boxes x 168g', 456000.00, 130),
(20, N'Orion ChocoPie', N'Carton 16 boxes x 12 pcs', 768000.00, 205);
SET IDENTITY_INSERT dbo.Item OFF;
GO

SET IDENTITY_INSERT dbo.Agent ON;
INSERT INTO dbo.Agent (AgentID, AgentName, Address, Phone) VALUES
(1, N'Minh Phat Trading Co.', N'125 Nguyen Trai, Ben Thanh Ward, District 1, Ho Chi Minh City', N'0283 822 1456'),
(2, N'Hoang Long Distribution', N'48 Le Van Sy, Ward 13, Phu Nhuan District, Ho Chi Minh City', N'0283 844 7712'),
(3, N'Thanh Dat Grocery Wholesale', N'210 Tran Hung Dao, Ward 2, District 5, Ho Chi Minh City', N'0909 315 228'),
(4, N'An Khang Mart', N'77 Vo Van Ngan, Linh Chieu Ward, Thu Duc City', N'0918 442 670'),
(5, N'Phuong Nam Supply', N'15 Ly Thuong Kiet, Ward 7, Tan Binh District, Ho Chi Minh City', N'0283 977 3301'),
(6, N'Saigon Fresh Foods', N'302 Cong Hoa, Ward 13, Tan Binh District, Ho Chi Minh City', N'0903 128 559'),
(7, N'Binh Minh Retail Group', N'9 Nguyen Van Linh, Tan Phong Ward, District 7, Ho Chi Minh City', N'0283 771 9085'),
(8, N'Dong A Convenience Stores', N'56 Phan Dang Luu, Ward 5, Binh Thanh District, Ho Chi Minh City', N'0938 664 102'),
(9, N'Mekong Delta Traders', N'88 30/4 Street, Xuan Khanh Ward, Ninh Kieu District, Can Tho', N'0292 383 4417'),
(10, N'Bien Hoa General Store', N'140 Pham Van Thuan, Tan Mai Ward, Bien Hoa City, Dong Nai', N'0251 382 6620'),
(11, N'Vung Tau Seaside Mart', N'33 Thuy Van, Ward 2, Vung Tau City, Ba Ria - Vung Tau', N'0254 352 8814'),
(12, N'Thu Dau Mot Wholesale', N'71 Yersin, Phu Cuong Ward, Thu Dau Mot City, Binh Duong', N'0274 382 1193'),
(13, N'Hai Chau Food Distribution', N'112 Nguyen Van Linh, Nam Duong Ward, Hai Chau District, Da Nang', N'0236 365 2078'),
(14, N'Hue Imperial Grocery', N'25 Hung Vuong, Phu Hoi Ward, Hue City', N'0234 382 5546'),
(15, N'Nha Trang Coastal Supply', N'64 Tran Phu, Loc Tho Ward, Nha Trang City, Khanh Hoa', N'0258 352 9031'),
(16, N'Da Lat Highland Traders', N'18 Phan Dinh Phung, Ward 2, Da Lat City, Lam Dong', N'0263 382 2765'),
(17, N'Long Xuyen Minimart Chain', N'45 Tran Hung Dao, My Binh Ward, Long Xuyen City, An Giang', N'0296 385 4120'),
(18, N'Tay Ninh Family Store', N'92 Cach Mang Thang Tam, Ward 3, Tay Ninh City, Tay Ninh', N'0276 382 7748');
SET IDENTITY_INSERT dbo.Agent OFF;
GO

SET IDENTITY_INSERT dbo.Users ON;
-- Passwords are PBKDF2-SHA256 hashes. Plain text: admins = Admin@123, agents = Agent@123
INSERT INTO dbo.Users (UserID, UserName, email, password, [lock], Role, FailedLoginAttempts, AgentID) VALUES
(1, N'admin', N'admin@b2bwholesale.vn', N'PBKDF2$100000$1Muqw0yXNgid2/0+tLwKMw==$KoDHuwLB1T5LBN6UHO7lN4i3y5ZYvS5pEFIrKgmyda0=', 0, N'Admin', 0, NULL),
(2, N'warehouse.lead', N'warehouse.lead@b2bwholesale.vn', N'PBKDF2$100000$oV9TJGuAyFs4IuCCLldqig==$gepVpG9rhcJ+LEVnoAyr+dX47j2FtYXwoKs8zKP7mB0=', 0, N'Admin', 0, NULL),
(3, N'agent01', N'minh.phat@agent.vn', N'PBKDF2$100000$vsOcmIlvlB8qT2xRot2+Ww==$6QMbOtbDZqlQSQQd77aUJ2ClfFxMKa60SKBx5oGvidM=', 0, N'Agent', 0, 1),
(4, N'agent02', N'hoang.long@agent.vn', N'PBKDF2$100000$rYGhyculuH8XZpclfInTJQ==$aB7GNSxtjPyOfl8p9WOm+Dr9KRkouur2S/lGEA398Vw=', 0, N'Agent', 0, 2),
(5, N'agent03', N'thanh.dat@agent.vn', N'PBKDF2$100000$GXKNWF2dj3LxcPWiOzuSxQ==$0sqiMC7LVdZ3eQW+jJod4VYxckvn/JtcnqY0MsgA318=', 0, N'Agent', 0, 3),
(6, N'agent04', N'an.khang@agent.vn', N'PBKDF2$100000$RSiVSpgLQ9O5mAYFdHkqvQ==$mPI/bLvyl+ym7w3Wc9OqPLvwr6WxgmrbKrm7EqgE3Fc=', 0, N'Agent', 0, 4),
(7, N'agent05', N'phuong.nam@agent.vn', N'PBKDF2$100000$yNiKx7h5enMrKA359mULhA==$zd6KYfZFMAphyLK/dpOm8qWJndsYiwsEU9MYs91f2fM=', 0, N'Agent', 0, 5),
(8, N'agent06', N'saigon.fresh@agent.vn', N'PBKDF2$100000$tO+6IdaVqfNJ3uU08xYgNQ==$M43gIBnUXyNFzJhu8pIoc2uy+NsPYr+1eOssw5PlV5U=', 0, N'Agent', 0, 6),
(9, N'agent07', N'binh.minh@agent.vn', N'PBKDF2$100000$2jQIM5LK2yaEIOdhwfCEiQ==$QMkwdmf7lU6410GtgnsWlHpEpxCUVp3EgXsvP2xV0Vs=', 0, N'Agent', 0, 7),
(10, N'agent08', N'dong.a@agent.vn', N'PBKDF2$100000$3FNltM+I0BUaLh4Ip0m8QQ==$4bLvvxDsxehHNBS2cPFFuEv8uW1y+VOxkbpJisDm2WI=', 0, N'Agent', 0, 8),
(11, N'agent09', N'mekong.delta@agent.vn', N'PBKDF2$100000$2y4ylPaj0PYAdfiYrVcGCA==$ojjR8Uy9RS7V3V1y8tagr8gyPz1AxKsSv1FwryziR3U=', 0, N'Agent', 0, 9),
(12, N'agent10', N'bien.hoa@agent.vn', N'PBKDF2$100000$OC4qeC2jxThxfTrosk3l/Q==$YdufIao+u4D0gi8vsr0nCtOkrhi/q3q+OZ3D96zsOl4=', 0, N'Agent', 0, 10),
(13, N'agent11', N'vung.tau@agent.vn', N'PBKDF2$100000$ry1VJK/SWWtPssKdlvZJOQ==$bLXqgf/uswmVzJdSbMEC6AMI6Rrh6hga2w+GuazQ/+Q=', 0, N'Agent', 0, 11),
(14, N'agent12', N'thu.dau@agent.vn', N'PBKDF2$100000$hUkaP5NLn+rVVwobj2bn6Q==$fuFBDieVg2tqk+2J26pFYtnIla0mqzbK7qGGVx73MZc=', 0, N'Agent', 0, 12),
(15, N'agent13', N'hai.chau@agent.vn', N'PBKDF2$100000$BUwEYq3/WsBPr1s0+wUZTQ==$/9jLxEomas145BP3s1WnoqrXadagcptLdXQdT0pj8SQ=', 0, N'Agent', 0, 13),
(16, N'agent14', N'hue.imperial@agent.vn', N'PBKDF2$100000$330be9kUdphgQ38ITVZGuA==$k8zscbw7xW0166E3dvh/Lt0NsXSuXRK6pYEz0G7glFE=', 0, N'Agent', 0, 14),
(17, N'agent15', N'nha.trang@agent.vn', N'PBKDF2$100000$MetrtVK/AIL5PMcDlTgUtw==$6wJSq3n9vg/Ht16NuZNJd4jcTPWERG+ZEW0iYYWAlqs=', 0, N'Agent', 0, 15),
(18, N'agent16', N'da.lat@agent.vn', N'PBKDF2$100000$3UC2y1J91UEfIjZHt64qQg==$gBLAlblHylA0ZrLgwrCG1aWIhfnRK9o/VYyDNyPqBOc=', 0, N'Agent', 0, 16),
(19, N'agent17', N'long.xuyen@agent.vn', N'PBKDF2$100000$J8QP80eH4LcpV3fSiIxrVA==$vbGcxhkeo0eNaWblaIgi19oQAEwkzLlv4lXLCwiuaQk=', 0, N'Agent', 0, 17),
(20, N'agent18', N'tay.ninh@agent.vn', N'PBKDF2$100000$/pH5C87XLMF+gzVBp9NR0g==$qgcm8GisuKjt9k9neLvkRdJG5Ep9HTNYnK9NRwDxf6M=', 1, N'Agent', 0, 18);
SET IDENTITY_INSERT dbo.Users OFF;
GO

SET IDENTITY_INSERT dbo.[Order] ON;
INSERT INTO dbo.[Order] (OrderID, OrderDate, AgentID, OrderStatus) VALUES
(1, '2026-09-03 09:15:00', 1, N'Delivered'),
(2, '2026-09-05 14:40:00', 3, N'Delivered'),
(3, '2026-09-10 10:05:00', 2, N'Delivered'),
(4, '2026-09-14 16:20:00', 5, N'Cancelled'),
(5, '2026-09-18 08:50:00', 9, N'Delivered'),
(6, '2026-09-22 11:30:00', 4, N'Delivered'),
(7, '2026-09-27 15:10:00', 1, N'Delivered'),
(8, '2026-10-02 09:45:00', 13, N'Delivered'),
(9, '2026-10-06 13:25:00', 6, N'Shipped'),
(10, '2026-10-11 10:00:00', 7, N'Shipped'),
(11, '2026-10-15 17:05:00', 2, N'Shipped'),
(12, '2026-10-20 08:30:00', 10, N'Confirmed'),
(13, '2026-10-24 14:15:00', 11, N'Confirmed'),
(14, '2026-10-28 09:55:00', 1, N'Confirmed'),
(15, '2026-11-02 10:40:00', 15, N'Pending'),
(16, '2026-11-05 16:35:00', 3, N'Pending'),
(17, '2026-11-08 11:20:00', 8, N'Pending'),
(18, '2026-11-10 09:10:00', 12, N'Pending'),
(19, '2026-11-12 15:45:00', 14, N'Pending'),
(20, '2026-11-14 08:25:00', 16, N'Pending');
SET IDENTITY_INSERT dbo.[Order] OFF;
GO

SET IDENTITY_INSERT dbo.OrderDetail ON;
INSERT INTO dbo.OrderDetail (ID, OrderID, ItemID, Quantity, UnitAmount) VALUES
(1, 1, 1, 40, 365000.00),
(2, 1, 7, 25, 215000.00),
(3, 2, 11, 60, 118000.00),
(4, 2, 15, 20, 395000.00),
(5, 3, 9, 30, 395000.00),
(6, 3, 5, 50, 98000.00),
(7, 4, 13, 10, 875000.00),
(8, 5, 1, 35, 365000.00),
(9, 5, 12, 40, 245000.00),
(10, 6, 3, 8, 1150000.00),
(11, 6, 20, 12, 768000.00),
(12, 7, 7, 30, 215000.00),
(13, 7, 11, 45, 118000.00),
(14, 8, 16, 6, 1050000.00),
(15, 8, 14, 10, 610000.00),
(16, 9, 6, 40, 105000.00),
(17, 10, 17, 5, 1290000.00),
(18, 10, 18, 15, 342000.00),
(19, 11, 2, 25, 389000.00),
(20, 12, 9, 20, 395000.00),
(21, 13, 19, 10, 456000.00),
(22, 14, 1, 30, 365000.00),
(23, 14, 4, 6, 1320000.00),
(24, 15, 10, 12, 485000.00),
(25, 16, 5, 80, 98000.00),
(26, 17, 8, 20, 205000.00),
(27, 17, 11, 30, 118000.00),
(28, 18, 13, 8, 875000.00),
(29, 19, 15, 18, 395000.00),
(30, 20, 20, 15, 768000.00);
SET IDENTITY_INSERT dbo.OrderDetail OFF;
GO

-- ===== QUICK CHECK =====
SELECT 'Item' AS [Table], COUNT(*) AS [Rows] FROM dbo.Item UNION ALL
SELECT 'Agent', COUNT(*) FROM dbo.Agent UNION ALL
SELECT 'Order', COUNT(*) FROM dbo.[Order] UNION ALL
SELECT 'OrderDetail', COUNT(*) FROM dbo.OrderDetail UNION ALL
SELECT 'Users', COUNT(*) FROM dbo.Users;
GO
