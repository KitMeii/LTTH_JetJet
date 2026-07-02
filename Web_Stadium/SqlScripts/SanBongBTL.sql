-- ============================================================
--  PitchHub.vn — Database v3 HOÀN CHỈNH
--  Bổ sung so với v2:
--    1. SanBongs        : thêm IsHidden
--    2. KhungGios       : thêm LoaiNgay (ThuThuong/CuoiTuan)
--    3. DatSans         : thêm StaffCheckInId, StaffCheckOutId,
--                         GhiChuSuCo, LoaiSuCo
--    4. StaffSanPhanCong: BẢNG MỚI — Staff được gán sân nào
--    5. DanhMucLoaiSan  : BẢNG MỚI — thay thế CHECK constraint
--    6. DanhMucLoaiCo   : BẢNG MỚI — thay thế CHECK constraint
--    7. Seed Data       : hướng dẫn hash BCrypt đúng cách
-- ============================================================

USE master;
GO

IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'SanBongBTL')
    CREATE DATABASE SanBongBTL;
GO

USE SanBongBTL;
GO

-- ============================================================
-- XÓA BẢNG CŨ (thứ tự FK: con trước, cha sau)
-- ============================================================
IF OBJECT_ID('StaffSanPhanCong','U') IS NOT NULL DROP TABLE StaffSanPhanCong;
IF OBJECT_ID('AuditLogs',       'U') IS NOT NULL DROP TABLE AuditLogs;
IF OBJECT_ID('KhieuNais',       'U') IS NOT NULL DROP TABLE KhieuNais;
IF OBJECT_ID('DatSan_DichVus',  'U') IS NOT NULL DROP TABLE DatSan_DichVus;
IF OBJECT_ID('Matchmakings',    'U') IS NOT NULL DROP TABLE Matchmakings;
IF OBJECT_ID('DatSans',         'U') IS NOT NULL DROP TABLE DatSans;
IF OBJECT_ID('DanhGias',        'U') IS NOT NULL DROP TABLE DanhGias;
IF OBJECT_ID('DichVus',         'U') IS NOT NULL DROP TABLE DichVus;
IF OBJECT_ID('KhungGios',       'U') IS NOT NULL DROP TABLE KhungGios;
IF OBJECT_ID('SanBongs',        'U') IS NOT NULL DROP TABLE SanBongs;
IF OBJECT_ID('Users',           'U') IS NOT NULL DROP TABLE Users;
IF OBJECT_ID('DanhMucDichVu',   'U') IS NOT NULL DROP TABLE DanhMucDichVu;
IF OBJECT_ID('DanhMucLoaiCo',   'U') IS NOT NULL DROP TABLE DanhMucLoaiCo;
IF OBJECT_ID('DanhMucLoaiSan',  'U') IS NOT NULL DROP TABLE DanhMucLoaiSan;
IF OBJECT_ID('DanhMucQuan',     'U') IS NOT NULL DROP TABLE DanhMucQuan;
GO

-- ============================================================
-- TẦNG 1: MASTER DATA (Admin quản lý, không phụ thuộc bảng nào)
-- ============================================================

-- Danh mục quận/huyện
CREATE TABLE DanhMucQuan (
    Id       INT           IDENTITY(1,1) PRIMARY KEY,
    TenQuan  NVARCHAR(100) NOT NULL UNIQUE,
    ThanhPho NVARCHAR(100) NOT NULL DEFAULT N'Hà Nội',
    ThuTu    INT           NOT NULL DEFAULT 0,
    IsActive BIT           NOT NULL DEFAULT 1
);
GO

-- Danh mục loại sân — Admin thêm được loại mới mà không cần sửa DB
CREATE TABLE DanhMucLoaiSan (
    Id       INT          IDENTITY(1,1) PRIMARY KEY,
    MaLoai   NVARCHAR(5)  NOT NULL UNIQUE,   -- '5' | '7' | '11'
    TenLoai  NVARCHAR(50) NOT NULL,          -- 'Sân 5 người' | ...
    IsActive BIT          NOT NULL DEFAULT 1
);
GO

-- Danh mục loại cỏ — Admin thêm được loại mới
CREATE TABLE DanhMucLoaiCo (
    Id      INT           IDENTITY(1,1) PRIMARY KEY,
    MaLoai  NVARCHAR(20)  NOT NULL UNIQUE,   -- 'Nhan tao' | 'Tu nhien'
    TenLoai NVARCHAR(100) NOT NULL,
    IsActive BIT          NOT NULL DEFAULT 1
);
GO

-- Danh mục dịch vụ gốc — Admin tạo, Owner bật/tắt cho sân của mình
CREATE TABLE DanhMucDichVu (
    Id        INT           IDENTITY(1,1) PRIMARY KEY,
    TenDichVu NVARCHAR(100) NOT NULL,
    Icon      NVARCHAR(10)  NULL,
    MoTa      NVARCHAR(500) NULL,
    IsActive  BIT           NOT NULL DEFAULT 1
);
GO

-- ============================================================
-- TẦNG 2: USERS
-- ============================================================
CREATE TABLE Users (
    Id                INT           IDENTITY(1,1) PRIMARY KEY,
    HoTen             NVARCHAR(100) NOT NULL,
    Email             NVARCHAR(150) NOT NULL UNIQUE,
    MatKhau           NVARCHAR(255) NOT NULL,        -- BCrypt hash
    SoDienThoai       NVARCHAR(20)  NULL,
    VaiTro            NVARCHAR(20)  NOT NULL DEFAULT 'User',
    IsActive          BIT           NOT NULL DEFAULT 1,
    -- NULL với Admin/User/Owner; có giá trị với Staff
    OwnerIdCuaStaff   INT           NULL,
    NgayTao           DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT CHK_Users_VaiTro CHECK (VaiTro IN ('User','Owner','Staff','Admin')),
    -- Staff tự tham chiếu về Owner trong cùng bảng Users
    CONSTRAINT FK_Users_OwnerCuaStaff FOREIGN KEY (OwnerIdCuaStaff) REFERENCES Users(Id)
);
GO

-- ============================================================
-- TẦNG 3: SAN BONG — thêm IsHidden
-- ============================================================
CREATE TABLE SanBongs (
    Id               INT           IDENTITY(1,1) PRIMARY KEY,
    TenSan           NVARCHAR(200) NOT NULL,
    DiaChi           NVARCHAR(300) NOT NULL,
    -- Lưu text TenQuan để filter nhanh, không cần JOIN DanhMucQuan mỗi lần
    Quan             NVARCHAR(100) NOT NULL,
    ThanhPho         NVARCHAR(100) NOT NULL,
    -- Lưu MaLoai để tương thích với code cũ
    LoaiSan          NVARCHAR(5)   NOT NULL,
    LoaiCo           NVARCHAR(50)  NOT NULL,
    HinhAnh          NVARCHAR(500) NULL,
    MoTa             NVARCHAR(MAX) NOT NULL DEFAULT '',
    DanhGiaTrungBinh FLOAT         NOT NULL DEFAULT 0,
    Latitude         FLOAT         NOT NULL DEFAULT 0,
    Longitude        FLOAT         NOT NULL DEFAULT 0,
    TrangThaiDuyet   NVARCHAR(20)  NOT NULL DEFAULT 'ChoDuyet',
    -- v2: Tỷ lệ cọc Owner tự cấu hình
    TyLeCoc          DECIMAL(3,2)  NOT NULL DEFAULT 0.30,
    -- v3: Owner tạm ẩn sân khi bảo trì (khác với TuChoi của Admin)
    IsHidden         BIT           NOT NULL DEFAULT 0,
    OwnerId          INT           NOT NULL,
    CONSTRAINT FK_SanBongs_Owner    FOREIGN KEY (OwnerId) REFERENCES Users(Id),
    CONSTRAINT CHK_SanBongs_Duyet   CHECK (TrangThaiDuyet IN ('ChoDuyet','DaDuyet','TuChoi')),
    CONSTRAINT CHK_SanBongs_TyLeCoc CHECK (TyLeCoc BETWEEN 0.10 AND 0.70)
);
GO

-- ============================================================
-- TẦNG 4: KHUNG GIO — thêm LoaiNgay để hỗ trợ Dynamic Pricing
-- ============================================================
CREATE TABLE KhungGios (
    Id                INT           IDENTITY(1,1) PRIMARY KEY,
    SanBongId         INT           NOT NULL,
    GioBatDau         TIME          NOT NULL,
    GioKetThuc        TIME          NOT NULL,
    Gia               DECIMAL(18,2) NOT NULL,       -- giá ngày thường
    GiaGioVang        DECIMAL(18,2) NOT NULL DEFAULT 0,  -- giá giờ cao điểm
    -- v3: phân biệt giá thứ thường / cuối tuần
    GiaCuoiTuan      DECIMAL(18,2) NOT NULL DEFAULT 0,
    -- v3: loại ngày áp dụng khung giờ này
    LoaiNgay          NVARCHAR(20)  NOT NULL DEFAULT 'TatCa',
    TrangThai         NVARCHAR(20)  NOT NULL DEFAULT 'Trong',
    ThoiGianHetGiuCho DATETIME      NULL,
    CONSTRAINT FK_KhungGios_SanBong    FOREIGN KEY (SanBongId) REFERENCES SanBongs(Id),
    CONSTRAINT CHK_KhungGios_TrangThai CHECK (TrangThai IN ('Trong','DaDat','DangGiu')),
    -- TatCa: áp dụng mọi ngày | ThuThuong: T2-T6 | CuoiTuan: T7-CN
    CONSTRAINT CHK_KhungGios_LoaiNgay  CHECK (LoaiNgay IN ('TatCa','ThuThuong','CuoiTuan'))
);
GO

-- ============================================================
-- TẦNG 5: DICH VU — gắn với sân cụ thể (kho của Owner)
-- ============================================================
CREATE TABLE DichVus (
    Id              INT           IDENTITY(1,1) PRIMARY KEY,
    SanBongId       INT           NOT NULL,
    DanhMucDichVuId INT           NOT NULL,
    TenDichVu       NVARCHAR(100) NOT NULL,
    Gia             DECIMAL(18,2) NOT NULL,
    TonKho          INT           NOT NULL DEFAULT 0,
    IsActive        BIT           NOT NULL DEFAULT 1,
    MoTa            NVARCHAR(500) NULL,
    CONSTRAINT FK_DichVus_SanBong  FOREIGN KEY (SanBongId)       REFERENCES SanBongs(Id),
    CONSTRAINT FK_DichVus_DanhMuc  FOREIGN KEY (DanhMucDichVuId) REFERENCES DanhMucDichVu(Id)
);
GO

-- ============================================================
-- TẦNG 6: DAT SAN
--   Thêm: StaffCheckInId, StaffCheckOutId, GhiChuSuCo, LoaiSuCo
--   Thêm trạng thái: DangSuDung
-- ============================================================
CREATE TABLE DatSans (
    Id              INT           IDENTITY(1,1) PRIMARY KEY,
    UserId          INT           NOT NULL,
    KhungGioId      INT           NOT NULL,
    NgayThiDau      DATETIME      NOT NULL,
    TienCoc         DECIMAL(18,2) NOT NULL,
    TongTien        DECIMAL(18,2) NOT NULL DEFAULT 0,
    MaXacNhan       NVARCHAR(50)  NOT NULL UNIQUE,
    TrangThai       NVARCHAR(20)  NOT NULL DEFAULT 'ChoDuyet',
    -- v3: Staff nào thực hiện check-in / check-out
    StaffCheckInId  INT           NULL,
    StaffCheckOutId INT           NULL,
    -- v3: Staff ghi nhận sự cố (No-show / hỏng hóc)
    LoaiSuCo        NVARCHAR(20)  NULL,   -- 'NoShow' | 'HongHoc' | NULL
    GhiChuSuCo      NVARCHAR(500) NULL,
    ThoiGianTao     DATETIME      NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_DatSans_User          FOREIGN KEY (UserId)          REFERENCES Users(Id),
    CONSTRAINT FK_DatSans_KhungGio      FOREIGN KEY (KhungGioId)      REFERENCES KhungGios(Id),
    CONSTRAINT FK_DatSans_StaffCheckIn  FOREIGN KEY (StaffCheckInId)  REFERENCES Users(Id),
    CONSTRAINT FK_DatSans_StaffCheckOut FOREIGN KEY (StaffCheckOutId) REFERENCES Users(Id),
    CONSTRAINT CHK_DatSans_TrangThai    CHECK (TrangThai IN (
        'ChoDuyet',    -- vừa đặt, chưa thanh toán cọc
        'DaXacNhan',   -- đã thanh toán cọc, chờ đến ngày đá
        'DangSuDung',  -- Staff check-in, đang đá
        'HoanThanh',   -- Staff check-out, thu đủ tiền
        'DaHuy'        -- đã hủy
    )),
    CONSTRAINT CHK_DatSans_LoaiSuCo CHECK (LoaiSuCo IN ('NoShow','HongHoc') OR LoaiSuCo IS NULL)
);
GO

-- ============================================================
-- TẦNG 7: STAFF SAN PHAN CONG — bảng quan hệ N-N
--   Owner gán Staff nào được quản lý sân nào
-- ============================================================
CREATE TABLE StaffSanPhanCong (
    Id        INT      IDENTITY(1,1) PRIMARY KEY,
    StaffId   INT      NOT NULL,
    SanBongId INT      NOT NULL,
    NgayGan   DATETIME NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_StaffSan_Staff   FOREIGN KEY (StaffId)   REFERENCES Users(Id),
    CONSTRAINT FK_StaffSan_SanBong FOREIGN KEY (SanBongId) REFERENCES SanBongs(Id),
    -- Mỗi Staff chỉ được gán vào 1 sân 1 lần
    CONSTRAINT UQ_StaffSan UNIQUE (StaffId, SanBongId)
);
GO

-- ============================================================
-- TẦNG 8: DATSAN_DICHVUS
-- ============================================================
CREATE TABLE DatSan_DichVus (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    DatSanId INT NOT NULL,
    DichVuId INT NOT NULL,
    SoLuong  INT NOT NULL DEFAULT 1,
    CONSTRAINT FK_DatSanDichVu_DatSan FOREIGN KEY (DatSanId) REFERENCES DatSans(Id),
    CONSTRAINT FK_DatSanDichVu_DichVu FOREIGN KEY (DichVuId) REFERENCES DichVus(Id)
);
GO

-- ============================================================
-- TẦNG 9: DANH GIA
--   Thêm DatSanId + UNIQUE constraint để mỗi User chỉ đánh giá 1 lần/đơn
-- ============================================================
CREATE TABLE DanhGias (
    Id          INT            IDENTITY(1,1) PRIMARY KEY,
    SanBongId   INT            NOT NULL,
    UserId      INT            NOT NULL,
    DatSanId    INT            NOT NULL,
    SoSao       INT            NOT NULL DEFAULT 5,
    NhanXet     NVARCHAR(1000) NULL,
    NgayDanhGia DATETIME       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_DanhGias_SanBong  FOREIGN KEY (SanBongId) REFERENCES SanBongs(Id),
    CONSTRAINT FK_DanhGias_User     FOREIGN KEY (UserId)    REFERENCES Users(Id),
    CONSTRAINT FK_DanhGias_DatSan   FOREIGN KEY (DatSanId)  REFERENCES DatSans(Id),
    CONSTRAINT CHK_DanhGias_SoSao   CHECK (SoSao BETWEEN 1 AND 5),
    CONSTRAINT UQ_DanhGia_User_DatSan UNIQUE (UserId, DatSanId)
);
GO

-- ============================================================
-- TẦNG 10: MATCHMAKINGS
-- ============================================================
CREATE TABLE Matchmakings (
    Id             INT            IDENTITY(1,1) PRIMARY KEY,
    DatSanId       INT            NOT NULL UNIQUE,
    UserId         INT            NOT NULL,
    TieuDe         NVARCHAR(200)  NOT NULL,
    MoTa           NVARCHAR(1000) NULL,
    SoNguoiCanThem INT            NOT NULL DEFAULT 1,
    TrangThai      NVARCHAR(20)   NOT NULL DEFAULT 'DangTim',
    NgayDang       DATETIME       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Matchmakings_DatSan     FOREIGN KEY (DatSanId) REFERENCES DatSans(Id),
    CONSTRAINT FK_Matchmakings_User       FOREIGN KEY (UserId)   REFERENCES Users(Id),
    CONSTRAINT CHK_Matchmakings_TrangThai CHECK (TrangThai IN ('DangTim','DaDu','DaDong'))
);
GO

-- ============================================================
-- TẦNG 11: KHIEU NAI — Admin xử lý hoàn cọc
-- ============================================================
CREATE TABLE KhieuNais (
    Id          INT            IDENTITY(1,1) PRIMARY KEY,
    DatSanId    INT            NOT NULL,
    UserId      INT            NOT NULL,
    LyDo        NVARCHAR(1000) NOT NULL,
    TrangThai   NVARCHAR(20)   NOT NULL DEFAULT 'ChoXuLy',
    GhiChuAdmin NVARCHAR(500)  NULL,
    SoTienHoan  DECIMAL(18,2)  NULL,
    NgayGui     DATETIME       NOT NULL DEFAULT GETDATE(),
    NgayXuLy    DATETIME       NULL,
    AdminXuLyId INT            NULL,
    CONSTRAINT FK_KhieuNais_DatSan FOREIGN KEY (DatSanId)    REFERENCES DatSans(Id),
    CONSTRAINT FK_KhieuNais_User   FOREIGN KEY (UserId)      REFERENCES Users(Id),
    CONSTRAINT FK_KhieuNais_Admin  FOREIGN KEY (AdminXuLyId) REFERENCES Users(Id),
    CONSTRAINT CHK_KhieuNais_TrangThai CHECK (TrangThai IN ('ChoXuLy','DaHoanCoc','TuChoi'))
);
GO

-- ============================================================
-- TẦNG 12: AUDIT LOGS — Bảo mật & hậu kiểm
-- ============================================================
CREATE TABLE AuditLogs (
    Id         INT            IDENTITY(1,1) PRIMARY KEY,
    UserId     INT            NOT NULL,
    VaiTro     NVARCHAR(20)   NOT NULL,
    HanhDong   NVARCHAR(100)  NOT NULL,
    DoiTuong   NVARCHAR(50)   NOT NULL,
    DoiTuongId INT            NOT NULL,
    MoTa       NVARCHAR(500)  NULL,
    IpAddress  NVARCHAR(50)   NULL,
    ThoiGian   DATETIME       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_AuditLogs_User FOREIGN KEY (UserId) REFERENCES Users(Id)
);
GO

-- ============================================================
-- INDEX — tối ưu hiệu năng truy vấn
-- ============================================================

-- Tìm kiếm sân
CREATE INDEX IX_SanBongs_Quan         ON SanBongs (Quan);
CREATE INDEX IX_SanBongs_TrangThai    ON SanBongs (TrangThaiDuyet);
CREATE INDEX IX_SanBongs_OwnerId      ON SanBongs (OwnerId);
CREATE INDEX IX_SanBongs_IsHidden     ON SanBongs (IsHidden);

-- Time-slot grid
CREATE INDEX IX_KhungGios_SanBongId   ON KhungGios (SanBongId);
CREATE INDEX IX_KhungGios_TrangThai   ON KhungGios (TrangThai);
CREATE INDEX IX_KhungGios_LoaiNgay    ON KhungGios (LoaiNgay);

-- Quản lý đơn
CREATE INDEX IX_DatSans_UserId        ON DatSans (UserId);
CREATE INDEX IX_DatSans_TrangThai     ON DatSans (TrangThai);
CREATE INDEX IX_DatSans_StaffCheckIn  ON DatSans (StaffCheckInId);
CREATE INDEX IX_DatSans_NgayThiDau    ON DatSans (NgayThiDau);

-- Dịch vụ
CREATE INDEX IX_DichVus_SanBongId     ON DichVus (SanBongId);

-- Staff phân công
CREATE INDEX IX_StaffSan_StaffId      ON StaffSanPhanCong (StaffId);
CREATE INDEX IX_StaffSan_SanBongId    ON StaffSanPhanCong (SanBongId);

-- Admin
CREATE INDEX IX_AuditLogs_UserId      ON AuditLogs (UserId);
CREATE INDEX IX_AuditLogs_ThoiGian    ON AuditLogs (ThoiGian);
CREATE INDEX IX_AuditLogs_HanhDong    ON AuditLogs (HanhDong);
CREATE INDEX IX_KhieuNais_TrangThai   ON KhieuNais (TrangThai);

-- Users
CREATE INDEX IX_Users_VaiTro          ON Users (VaiTro);
CREATE INDEX IX_Users_OwnerIdCuaStaff ON Users (OwnerIdCuaStaff);
CREATE INDEX IX_Matchmakings_TrangThai ON Matchmakings (TrangThai);
GO

-- ============================================================
-- SEED DATA
-- ============================================================

-- ⚠️  QUAN TRỌNG VỀ MẬT KHẨU:
--     Các chuỗi hash dưới đây là BCrypt của chuỗi tương ứng.
--     Để generate hash đúng, chạy đoạn code C# sau trong project:
--
--     using BCrypt.Net;
--     Console.WriteLine(BCrypt.HashPassword("admin123"));
--     Console.WriteLine(BCrypt.HashPassword("owner123"));
--     Console.WriteLine(BCrypt.HashPassword("user123"));
--     Console.WriteLine(BCrypt.HashPassword("staff123"));
--
--     Rồi thay thế 4 chuỗi BCRYPT_PLACEHOLDER bên dưới.
--     Tạm thời để plaintext để chạy thử, nhớ đổi lại trước khi demo.

DECLARE
    -- Master Data
    @qCauGiay   INT, @qDongDa   INT, @qHoangMai INT,
    @qLongBien  INT, @qNamTuLiem INT,
    @lsSan5     INT, @lsSan7    INT, @lsSan11   INT,
    @lcNhanTao  INT, @lcTuNhien INT,
    @dmNuoc     INT, @dmBong    INT, @dmTai     INT, @dmAo INT,
    -- Users
    @idAdmin    INT, @idOwner   INT, @idUser    INT, @idStaff INT,
    -- SanBongs
    @idSan1     INT, @idSan2    INT, @idSan3    INT,
    @idSan4     INT, @idSan5    INT,
    -- DichVus
    @dv1Nuoc    INT, @dv1Bong   INT, @dv1Tai    INT, @dv1Ao  INT,
    -- KhungGios
    @kgDat1     INT, @kgDat2    INT,
    -- DatSans
    @ds1        INT, @ds2       INT;

-- ── 1. MASTER DATA: QUẬN ─────────────────────────────────────
INSERT INTO DanhMucQuan (TenQuan, ThanhPho, ThuTu)
VALUES
    (N'Cầu Giấy',     N'Hà Nội', 1),
    (N'Đống Đa',      N'Hà Nội', 2),
    (N'Hoàng Mai',    N'Hà Nội', 3),
    (N'Long Biên',    N'Hà Nội', 4),
    (N'Nam Từ Liêm',  N'Hà Nội', 5),
    (N'Bắc Từ Liêm',  N'Hà Nội', 6),
    (N'Tây Hồ',       N'Hà Nội', 7),
    (N'Ba Đình',      N'Hà Nội', 8),
    (N'Hai Bà Trưng', N'Hà Nội', 9),
    (N'Thanh Xuân',   N'Hà Nội', 10);

SELECT @qCauGiay   = Id FROM DanhMucQuan WHERE TenQuan = N'Cầu Giấy';
SELECT @qDongDa    = Id FROM DanhMucQuan WHERE TenQuan = N'Đống Đa';
SELECT @qHoangMai  = Id FROM DanhMucQuan WHERE TenQuan = N'Hoàng Mai';
SELECT @qLongBien  = Id FROM DanhMucQuan WHERE TenQuan = N'Long Biên';
SELECT @qNamTuLiem = Id FROM DanhMucQuan WHERE TenQuan = N'Nam Từ Liêm';
PRINT N'✔ DanhMucQuan: 10 quận';

-- ── 2. MASTER DATA: LOẠI SÂN ──────────────────────────────────
INSERT INTO DanhMucLoaiSan (MaLoai, TenLoai) VALUES ('5',  N'Sân 5 người');
INSERT INTO DanhMucLoaiSan (MaLoai, TenLoai) VALUES ('7',  N'Sân 7 người');
INSERT INTO DanhMucLoaiSan (MaLoai, TenLoai) VALUES ('11', N'Sân 11 người');
SELECT @lsSan5  = Id FROM DanhMucLoaiSan WHERE MaLoai = '5';
SELECT @lsSan7  = Id FROM DanhMucLoaiSan WHERE MaLoai = '7';
SELECT @lsSan11 = Id FROM DanhMucLoaiSan WHERE MaLoai = '11';
PRINT N'✔ DanhMucLoaiSan: 3 loại';

-- ── 3. MASTER DATA: LOẠI CỎ ───────────────────────────────────
INSERT INTO DanhMucLoaiCo (MaLoai, TenLoai) VALUES ('Nhan tao', N'Cỏ nhân tạo');
INSERT INTO DanhMucLoaiCo (MaLoai, TenLoai) VALUES ('Tu nhien', N'Cỏ tự nhiên');
SELECT @lcNhanTao = Id FROM DanhMucLoaiCo WHERE MaLoai = 'Nhan tao';
SELECT @lcTuNhien = Id FROM DanhMucLoaiCo WHERE MaLoai = 'Tu nhien';
PRINT N'✔ DanhMucLoaiCo: 2 loại';

-- ── 4. MASTER DATA: DỊCH VỤ ──────────────────────────────────
INSERT INTO DanhMucDichVu (TenDichVu, Icon, MoTa)
VALUES
    (N'Nước uống',     N'💧', N'Nước lọc / nước ngọt / tăng lực'),
    (N'Thuê bóng',     N'⚽', N'Bóng thi đấu tiêu chuẩn Size 4/5'),
    (N'Thuê trọng tài',N'🟡', N'Trọng tài có kinh nghiệm'),
    (N'Thuê áo đấu',   N'👕', N'Áo thi đấu có số, 2 màu');
SELECT @dmNuoc = Id FROM DanhMucDichVu WHERE TenDichVu = N'Nước uống';
SELECT @dmBong = Id FROM DanhMucDichVu WHERE TenDichVu = N'Thuê bóng';
SELECT @dmTai  = Id FROM DanhMucDichVu WHERE TenDichVu = N'Thuê trọng tài';
SELECT @dmAo   = Id FROM DanhMucDichVu WHERE TenDichVu = N'Thuê áo đấu';
PRINT N'✔ DanhMucDichVu: 4 danh mục';

-- ── 5. USERS ──────────────────────────────────────────────────
-- ⚠️  Thay BCRYPT_HASH_xxx bằng hash thật từ BCrypt.Net trước khi chạy
-- Tạm thời để plaintext để demo, nhớ đổi trước khi nộp bài

INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive)
VALUES (N'Admin PitchHub', 'admin@pitchhub.vn',
        '$2a$11$f0tXD6o7XYAs7/tE0nx4Reiw1.84L2ItgL0tRwE1Bq.GZbE8MMuzS',   -- đổi thành BCrypt hash của "admin123"
        '0901000001', 'Admin', 1);
SET @idAdmin = SCOPE_IDENTITY();

INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive)
VALUES (N'Vũ Nguyễn Tuấn Kiệt', 'owner1@gmail.com',
        '$2a$11$dZvxGdl0dNQWsvIM4IO2VuM4kGwP60qFmpbIOKvD0iLulA00/4cCW',   -- đổi thành BCrypt hash của "owner123"
        '0901000002', 'Owner', 1);
SET @idOwner = SCOPE_IDENTITY();

INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive)
VALUES (N'Nguyễn Công Nam', 'user1@gmail.com',
        '$2a$11$wmJXgVs5/RBXU2y/vP4Bs.UcgtzPg0r4Iv2t3DgsZvDrOKD32vPzO',    -- đổi thành BCrypt hash của "user123"
        '0901000003', 'User', 1);
SET @idUser = SCOPE_IDENTITY();

-- Staff: OwnerIdCuaStaff trỏ về @idOwner
INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive, OwnerIdCuaStaff)
VALUES (N'Đào Việt Toàn', 'staff@pitchhub.vn',
        '$2a$11$oOUcIEMbESDcI5QECnfcBOzJtTAAsWbRM3ZX7KQhH3XIWWdmtfR1S',   -- đổi thành BCrypt hash của "staff123"
        '0901000004', 'Staff', 1, @idOwner);
SET @idStaff = SCOPE_IDENTITY();

PRINT CONCAT(N'✔ Users: Admin=', @idAdmin,
             ' Owner=', @idOwner,
             ' User=',  @idUser,
             ' Staff=', @idStaff);

-- ── 6. SAN BONG ───────────────────────────────────────────────
INSERT INTO SanBongs (TenSan, DiaChi, Quan, ThanhPho, LoaiSan, LoaiCo,
                      MoTa, DanhGiaTrungBinh, Latitude, Longitude,
                      TrangThaiDuyet, TyLeCoc, IsHidden, OwnerId)
VALUES (N'Sân Cầu Giấy Sport', N'12 Xuân Thủy',
        N'Cầu Giấy', N'Hà Nội', '5', 'Nhan tao',
        N'Sân cỏ nhân tạo chất lượng cao, đèn chiếu sáng ban đêm.',
        4.5, 21.0362, 105.7826, 'DaDuyet', 0.30, 0, @idOwner);
SET @idSan1 = SCOPE_IDENTITY();

INSERT INTO SanBongs (TenSan, DiaChi, Quan, ThanhPho, LoaiSan, LoaiCo,
                      MoTa, DanhGiaTrungBinh, Latitude, Longitude,
                      TrangThaiDuyet, TyLeCoc, IsHidden, OwnerId)
VALUES (N'Sân Đống Đa Arena', N'45 Tây Sơn',
        N'Đống Đa', N'Hà Nội', '7', 'Nhan tao',
        N'Sân 7 người rộng rãi, có mái che, chỗ để xe rộng.',
        4.2, 21.0198, 105.8412, 'DaDuyet', 0.30, 0, @idOwner);
SET @idSan2 = SCOPE_IDENTITY();

INSERT INTO SanBongs (TenSan, DiaChi, Quan, ThanhPho, LoaiSan, LoaiCo,
                      MoTa, DanhGiaTrungBinh, Latitude, Longitude,
                      TrangThaiDuyet, TyLeCoc, IsHidden, OwnerId)
VALUES (N'Sân Hoàng Mai FC', N'78 Giải Phóng',
        N'Hoàng Mai', N'Hà Nội', '5', 'Tu nhien',
        N'Sân cỏ tự nhiên thoáng mát, phù hợp thi đấu buổi sáng.',
        3.8, 20.9876, 105.8543, 'DaDuyet', 0.50, 0, @idOwner);
SET @idSan3 = SCOPE_IDENTITY();

INSERT INTO SanBongs (TenSan, DiaChi, Quan, ThanhPho, LoaiSan, LoaiCo,
                      MoTa, DanhGiaTrungBinh, Latitude, Longitude,
                      TrangThaiDuyet, TyLeCoc, IsHidden, OwnerId)
VALUES (N'Sân Long Biên Star', N'23 Nguyễn Văn Cừ',
        N'Long Biên', N'Hà Nội', '11', 'Nhan tao',
        N'Sân 11 người tiêu chuẩn FIFA, có phòng thay đồ.',
        4.7, 21.0465, 105.8923, 'DaDuyet', 0.30, 0, @idOwner);
SET @idSan4 = SCOPE_IDENTITY();

INSERT INTO SanBongs (TenSan, DiaChi, Quan, ThanhPho, LoaiSan, LoaiCo,
                      MoTa, DanhGiaTrungBinh, Latitude, Longitude,
                      TrangThaiDuyet, TyLeCoc, IsHidden, OwnerId)
VALUES (N'Sân Chờ Duyệt Test', N'99 Test Street',
        N'Nam Từ Liêm', N'Hà Nội', '5', 'Nhan tao',
        N'Sân đang chờ Admin phê duyệt.',
        0, 21.0100, 105.7500, 'ChoDuyet', 0.30, 0, @idOwner);
SET @idSan5 = SCOPE_IDENTITY();

PRINT CONCAT(N'✔ SanBongs: ', @idSan1,' ',@idSan2,' ',@idSan3,' ',@idSan4,' ',@idSan5);

-- ── 7. STAFF PHAN CONG SAN ────────────────────────────────────
-- Staff được gán vào Sân 1 và Sân 2
INSERT INTO StaffSanPhanCong (StaffId, SanBongId) VALUES (@idStaff, @idSan1);
INSERT INTO StaffSanPhanCong (StaffId, SanBongId) VALUES (@idStaff, @idSan2);
PRINT N'✔ StaffSanPhanCong: Staff gán vào Sân 1 + Sân 2';

-- ── 8. KHUNG GIO ─────────────────────────────────────────────
-- Sân 1 — khung giờ thường + cuối tuần
INSERT INTO KhungGios (SanBongId, GioBatDau, GioKetThuc, Gia, GiaGioVang, GiaCuoiTuan, LoaiNgay, TrangThai)
VALUES
    (@idSan1,'06:00','07:30',180000,220000,200000,'TatCa',   'Trong'),
    (@idSan1,'07:30','09:00',180000,220000,200000,'TatCa',   'Trong'),
    (@idSan1,'09:00','10:30',160000,200000,180000,'TatCa',   'Trong'),
    (@idSan1,'15:00','16:30',160000,200000,180000,'TatCa',   'Trong'),
    (@idSan1,'16:30','18:00',200000,260000,240000,'TatCa',   'Trong'),
    (@idSan1,'18:00','19:30',250000,300000,320000,'TatCa',   'DaDat'),
    (@idSan1,'19:30','21:00',250000,300000,320000,'TatCa',   'Trong');
SELECT @kgDat1 = Id FROM KhungGios
WHERE SanBongId = @idSan1 AND GioBatDau = '18:00' AND TrangThai = 'DaDat';

-- Sân 2
INSERT INTO KhungGios (SanBongId, GioBatDau, GioKetThuc, Gia, GiaGioVang, GiaCuoiTuan, LoaiNgay, TrangThai)
VALUES
    (@idSan2,'06:00','07:30',250000,300000,280000,'TatCa','Trong'),
    (@idSan2,'07:30','09:00',250000,300000,280000,'TatCa','Trong'),
    (@idSan2,'17:00','18:30',320000,380000,400000,'TatCa','DaDat'),
    (@idSan2,'18:30','20:00',350000,420000,440000,'TatCa','Trong'),
    (@idSan2,'20:00','21:30',350000,420000,440000,'TatCa','Trong');
SELECT @kgDat2 = Id FROM KhungGios
WHERE SanBongId = @idSan2 AND GioBatDau = '17:00' AND TrangThai = 'DaDat';

-- Sân 3
INSERT INTO KhungGios (SanBongId, GioBatDau, GioKetThuc, Gia, GiaGioVang, GiaCuoiTuan, LoaiNgay, TrangThai)
VALUES
    (@idSan3,'05:30','07:00',150000,180000,170000,'TatCa','Trong'),
    (@idSan3,'07:00','08:30',150000,180000,170000,'TatCa','Trong'),
    (@idSan3,'16:00','17:30',180000,220000,200000,'TatCa','Trong'),
    (@idSan3,'17:30','19:00',200000,250000,230000,'TatCa','Trong');

-- Sân 4
INSERT INTO KhungGios (SanBongId, GioBatDau, GioKetThuc, Gia, GiaGioVang, GiaCuoiTuan, LoaiNgay, TrangThai)
VALUES
    (@idSan4,'06:00','08:00',500000,600000,580000,'TatCa','Trong'),
    (@idSan4,'08:00','10:00',500000,600000,580000,'TatCa','Trong'),
    (@idSan4,'15:00','17:00',550000,650000,620000,'TatCa','Trong'),
    (@idSan4,'17:00','19:00',650000,780000,750000,'TatCa','Trong'),
    (@idSan4,'19:00','21:00',650000,780000,750000,'TatCa','Trong');

PRINT CONCAT(N'✔ KhungGios xong. kgDat1=', @kgDat1, ' kgDat2=', @kgDat2);

-- ── 9. DICH VU CUA SAN 1 ─────────────────────────────────────
INSERT INTO DichVus (SanBongId, DanhMucDichVuId, TenDichVu, Gia, TonKho, MoTa)
VALUES
    (@idSan1, @dmNuoc, N'Nước uống',     15000,  100, N'Nước lọc / nước ngọt'),
    (@idSan1, @dmBong, N'Thuê bóng',     30000,   10, N'Bóng Size 4/5'),
    (@idSan1, @dmTai,  N'Thuê trọng tài',100000,   3, N'Trọng tài kinh nghiệm'),
    (@idSan1, @dmAo,   N'Thuê áo đấu',   20000,   22, N'Áo 2 màu có số');
SELECT @dv1Nuoc = Id FROM DichVus WHERE SanBongId = @idSan1 AND DanhMucDichVuId = @dmNuoc;
SELECT @dv1Bong = Id FROM DichVus WHERE SanBongId = @idSan1 AND DanhMucDichVuId = @dmBong;
SELECT @dv1Tai  = Id FROM DichVus WHERE SanBongId = @idSan1 AND DanhMucDichVuId = @dmTai;
SELECT @dv1Ao   = Id FROM DichVus WHERE SanBongId = @idSan1 AND DanhMucDichVuId = @dmAo;
PRINT N'✔ DichVus Sân 1 xong';

-- ── 10. DAT SAN ───────────────────────────────────────────────
-- Đơn 1: DaXacNhan — đã thanh toán cọc, chờ đến ngày đá
INSERT INTO DatSans (UserId, KhungGioId, NgayThiDau, TienCoc, TongTien, MaXacNhan, TrangThai)
VALUES (@idUser, @kgDat1, DATEADD(DAY,2,GETDATE()), 75000, 250000, 'ABC12345', 'DaXacNhan');
SET @ds1 = SCOPE_IDENTITY();

-- Đơn 2: ChoDuyet — vừa đặt, chưa thanh toán
INSERT INTO DatSans (UserId, KhungGioId, NgayThiDau, TienCoc, TongTien, MaXacNhan, TrangThai)
VALUES (@idUser, @kgDat2, DATEADD(DAY,3,GETDATE()), 105000, 0, 'DEF67890', 'ChoDuyet');
SET @ds2 = SCOPE_IDENTITY();

PRINT CONCAT(N'✔ DatSans: ds1=', @ds1, ' ds2=', @ds2);

-- ── 11. DICH VU KEM THEO ─────────────────────────────────────
INSERT INTO DatSan_DichVus (DatSanId, DichVuId, SoLuong)
VALUES
    (@ds1, @dv1Nuoc, 10),
    (@ds1, @dv1Bong,  1),
    (@ds1, @dv1Tai,   1);

-- ── 12. DANH GIA ─────────────────────────────────────────────
-- Chỉ demo — thực tế controller phải check TrangThai = 'HoanThanh' trước
INSERT INTO DanhGias (SanBongId, UserId, DatSanId, SoSao, NhanXet)
VALUES (@idSan1, @idUser, @ds1, 5, N'Sân rất đẹp, cỏ mới, nhân viên nhiệt tình!');

UPDATE SanBongs SET DanhGiaTrungBinh = 5.0 WHERE Id = @idSan1;
UPDATE SanBongs SET DanhGiaTrungBinh = 4.2 WHERE Id = @idSan2;
UPDATE SanBongs SET DanhGiaTrungBinh = 3.8 WHERE Id = @idSan3;
UPDATE SanBongs SET DanhGiaTrungBinh = 4.7 WHERE Id = @idSan4;

-- ── 13. MATCHMAKING ──────────────────────────────────────────
INSERT INTO Matchmakings (DatSanId, UserId, TieuDe, MoTa, SoNguoiCanThem, TrangThai)
VALUES (@ds1, @idUser,
        N'Cần thêm 2 tiền đạo — Sân Cầu Giấy 18h tối mai',
        N'Team trình độ trung bình, chơi vui là chính. LH ngay!',
        2, 'DangTim');

-- ── 14. AUDIT LOG MẪU ────────────────────────────────────────
INSERT INTO AuditLogs (UserId, VaiTro, HanhDong, DoiTuong, DoiTuongId, MoTa)
VALUES
    (@idAdmin, 'Admin', 'PheDuyetSan', 'SanBong', @idSan1, N'Admin phê duyệt: Sân Cầu Giấy Sport'),
    (@idAdmin, 'Admin', 'PheDuyetSan', 'SanBong', @idSan2, N'Admin phê duyệt: Sân Đống Đa Arena'),
    (@idAdmin, 'Admin', 'PheDuyetSan', 'SanBong', @idSan3, N'Admin phê duyệt: Sân Hoàng Mai FC'),
    (@idAdmin, 'Admin', 'PheDuyetSan', 'SanBong', @idSan4, N'Admin phê duyệt: Sân Long Biên Star');

-- ============================================================
-- KIỂM TRA TỔNG
-- ============================================================
SELECT [Bang] = v.n, [So ban ghi] = v.c FROM (VALUES
    ('DanhMucQuan',      (SELECT COUNT(*) FROM DanhMucQuan)),
    ('DanhMucLoaiSan',   (SELECT COUNT(*) FROM DanhMucLoaiSan)),
    ('DanhMucLoaiCo',    (SELECT COUNT(*) FROM DanhMucLoaiCo)),
    ('DanhMucDichVu',    (SELECT COUNT(*) FROM DanhMucDichVu)),
    ('Users',            (SELECT COUNT(*) FROM Users)),
    ('SanBongs',         (SELECT COUNT(*) FROM SanBongs)),
    ('StaffSanPhanCong', (SELECT COUNT(*) FROM StaffSanPhanCong)),
    ('KhungGios',        (SELECT COUNT(*) FROM KhungGios)),
    ('DichVus',          (SELECT COUNT(*) FROM DichVus)),
    ('DatSans',          (SELECT COUNT(*) FROM DatSans)),
    ('DatSan_DichVus',   (SELECT COUNT(*) FROM DatSan_DichVus)),
    ('DanhGias',         (SELECT COUNT(*) FROM DanhGias)),
    ('Matchmakings',     (SELECT COUNT(*) FROM Matchmakings)),
    ('KhieuNais',        (SELECT COUNT(*) FROM KhieuNais)),
    ('AuditLogs',        (SELECT COUNT(*) FROM AuditLogs))
) v(n,c);

PRINT '=========================================================';
PRINT N'✅ Database SanBongBTL v3 tạo thành công!';
PRINT N'';
PRINT N'⚠️  NHẮC NHỞ QUAN TRỌNG:';
PRINT N'    Thay 4 chuỗi BCRYPT_HASH_xxx bằng hash thật từ BCrypt.Net';
PRINT N'    trước khi chạy file này lần cuối để demo/nộp bài.';
PRINT N'';
PRINT N'Tài khoản test (sau khi cập nhật hash):';
PRINT N'  Admin : admin@pitchhub.vn  / admin123';
PRINT N'  Owner : owner1@gmail.com   / owner123';
PRINT N'  User  : user1@gmail.com    / user123';
PRINT N'  Staff : staff@pitchhub.vn  / staff123';
PRINT '=========================================================';
GO

-- ============================================================
--  PitchHub — SQL PATCH v4
--  Bổ sung:
--    1. Bảng VungKhuVuc (phân vùng hoa hồng linh hoạt)
--    2. DanhMucQuan thêm VungKhuVucId (FK)
--    3. SanBongs thêm DaKyHopDong, NgayKyHopDong, NoiDungHopDong
--  Chạy SAU SanBongBTL_v3.sql
-- ============================================================

USE SanBongBTL;
GO

-- ── 1. BẢNG VUNGKHUVUC ──────────────────────────────────────
-- Admin tạo/sửa/xoá qua giao diện — không cần code lại
CREATE TABLE VungKhuVucs (
    Id             INT           IDENTITY(1,1) PRIMARY KEY,
    TenVung        NVARCHAR(100) NOT NULL UNIQUE,
    MoTa           NVARCHAR(300) NULL,
    -- Tỷ lệ hoa hồng áp dụng cho toàn vùng
    TyLeHoaHong    DECIMAL(3,2)  NOT NULL DEFAULT 0.10,
    -- Màu sắc hiển thị trên bản đồ và UI (hex color)
    MauSac         NVARCHAR(10)  NOT NULL DEFAULT '#1ed760',
    -- Tọa độ trung tâm vùng (để bản đồ tự zoom về)
    Lat            FLOAT         NOT NULL DEFAULT 21.0285,
    Lng            FLOAT         NOT NULL DEFAULT 105.8542,
    DefaultZoom    INT           NOT NULL DEFAULT 12,
    ThuTu          INT           NOT NULL DEFAULT 0,
    IsActive       BIT           NOT NULL DEFAULT 1,
    CONSTRAINT CHK_VungKhuVuc_TyLe CHECK (TyLeHoaHong BETWEEN 0.01 AND 0.50)
);
GO

-- ── 2. CẬP NHẬT DanhMucQuan — thêm VungKhuVucId ─────────────
ALTER TABLE DanhMucQuan
ADD VungKhuVucId INT NULL,
    CONSTRAINT FK_DanhMucQuan_Vung
        FOREIGN KEY (VungKhuVucId) REFERENCES VungKhuVucs(Id);
GO

-- ── 3. CẬP NHẬT SanBongs — thêm trường hợp đồng ────────────
ALTER TABLE SanBongs
ADD DaKyHopDong    BIT           NOT NULL DEFAULT 0,
    NgayKyHopDong  DATETIME      NULL,
    -- Lưu snapshot nội dung HĐ lúc ký — không bao giờ thay đổi sau
    NoiDungHopDong NVARCHAR(MAX) NULL;
GO

-- ── 4. SEED: VungKhuVucs ─────────────────────────────────────
DECLARE @v1 INT, @v2 INT, @v3 INT, @v4 INT;

INSERT INTO VungKhuVucs (TenVung, MoTa, TyLeHoaHong, MauSac, Lat, Lng, DefaultZoom, ThuTu)
VALUES (N'Nội ô trung tâm',
        N'Hoàn Kiếm, Ba Đình, Đống Đa, Hai Bà Trưng — khu vực trung tâm lịch sử',
        0.10, '#1ed760', 21.0285, 105.8542, 13, 1);
SET @v1 = SCOPE_IDENTITY();

INSERT INTO VungKhuVucs (TenVung, MoTa, TyLeHoaHong, MauSac, Lat, Lng, DefaultZoom, ThuTu)
VALUES (N'Nội ô mở rộng',
        N'Cầu Giấy, Tây Hồ, Thanh Xuân, Bắc Từ Liêm — vùng nội ô phát triển',
        0.08, '#6caee0', 21.0500, 105.7900, 12, 2);
SET @v2 = SCOPE_IDENTITY();

INSERT INTO VungKhuVucs (TenVung, MoTa, TyLeHoaHong, MauSac, Lat, Lng, DefaultZoom, ThuTu)
VALUES (N'Vùng ngoại ô',
        N'Hà Đông, Long Biên, Hoàng Mai, Nam Từ Liêm — vùng ven đô',
        0.06, '#f39c12', 20.9800, 105.8800, 11, 3);
SET @v3 = SCOPE_IDENTITY();

INSERT INTO VungKhuVucs (TenVung, MoTa, TyLeHoaHong, MauSac, Lat, Lng, DefaultZoom, ThuTu)
VALUES (N'Vùng xa trung tâm',
        N'Sơn Tây, Ba Vì, Mỹ Đức — vùng ngoại thành xa',
        0.05, '#e74c3c', 21.1200, 105.5000, 10, 4);
SET @v4 = SCOPE_IDENTITY();

-- ── 5. Gán Quận vào Vùng ─────────────────────────────────────
-- Nội ô trung tâm
UPDATE DanhMucQuan SET VungKhuVucId = @v1
WHERE TenQuan IN (N'Hoàn Kiếm', N'Ba Đình', N'Đống Đa', N'Hai Bà Trưng');

-- Nội ô mở rộng
UPDATE DanhMucQuan SET VungKhuVucId = @v2
WHERE TenQuan IN (N'Cầu Giấy', N'Tây Hồ', N'Thanh Xuân', N'Bắc Từ Liêm');

-- Ngoại ô
UPDATE DanhMucQuan SET VungKhuVucId = @v3
WHERE TenQuan IN (N'Hà Đông', N'Long Biên', N'Hoàng Mai', N'Nam Từ Liêm');

-- Vùng xa
-- (Sơn Tây chưa có trong seed DanhMucQuan — thêm nếu cần)

PRINT N'✔ VungKhuVucs: 4 vùng';
PRINT N'✔ DanhMucQuan: đã gán VungKhuVucId';
PRINT N'✔ SanBongs: đã thêm DaKyHopDong, NgayKyHopDong, NoiDungHopDong';
GO

-- Kiểm tra
SELECT q.TenQuan, v.TenVung, v.TyLeHoaHong, v.MauSac
FROM DanhMucQuan q
LEFT JOIN VungKhuVucs v ON q.VungKhuVucId = v.Id
ORDER BY v.ThuTu, q.ThuTu;
GO

--11/5/2026 update
-- 1. Gán tất cả dịch vụ hiện có cho sân số 1 và sân số 2 để kiểm tra
UPDATE DichVus 
SET SanBongId = 2, -- Gán cho sân số 2
    TonKho = 100,  -- Đảm bảo có hàng trong kho
    IsActive = 1   -- Đảm bảo dịch vụ đang hoạt động
WHERE SanBongId IS NULL;

-- 2. Kiểm tra lại dữ liệu xem đã khớp chưa
SELECT Id, TenDichVu, SanBongId, TonKho 
FROM DichVus;

-- ============================================================
-- MIGRATION: Tao bang AnhSanBongs cho Owner upload nhieu anh
-- Chay trong SSMS voi database SanBongBTL
-- ============================================================


--Bo sung 15/5/2026
USE SanBongBTL;
GO

-- Tao bang AnhSanBongs
IF NOT EXISTS (SELECT * FROM sys.objects WHERE name = 'AnhSanBongs' AND type = 'U')
BEGIN
    CREATE TABLE AnhSanBongs (
        Id          INT            IDENTITY(1,1) PRIMARY KEY,
        SanBongId   INT            NOT NULL,
        DuongDan    NVARCHAR(1000) NOT NULL,   -- URL hoac duong dan file
        LoaiAnh     NVARCHAR(20)   NOT NULL DEFAULT 'Upload',  -- 'Upload' | 'URL'
        ThuTu       INT            NOT NULL DEFAULT 0,          -- Thu tu hien thi
        MoTa        NVARCHAR(200)  NULL,
        NgayThem    DATETIME       NOT NULL DEFAULT GETDATE(),
        IsActive    BIT            NOT NULL DEFAULT 1,

        CONSTRAINT FK_AnhSanBongs_SanBong
            FOREIGN KEY (SanBongId) REFERENCES SanBongs(Id) ON DELETE CASCADE
    );

    CREATE INDEX IX_AnhSanBongs_SanBong ON AnhSanBongs (SanBongId, ThuTu);
    PRINT N'Da tao bang AnhSanBongs thanh cong';
END
ELSE
    PRINT N'Bang AnhSanBongs da ton tai';
GO

ALTER TABLE SanBongs ADD
    ThoiGianGiuCho INT NOT NULL DEFAULT 15,
    ThoiGianHuyTruocGioDa INT NOT NULL DEFAULT 120,
    PhanTramHoanCocDungHan DECIMAL(3,2) NOT NULL DEFAULT 1.00,
    PhanTramHoanCocTreHan DECIMAL(3,2) NOT NULL DEFAULT 0.00;

ALTER TABLE DanhGias ADD PhanHoiOwner NVARCHAR(MAX) NULL, NgayPhanHoi DATETIME NULL;

USE [master];
GO
CREATE LOGIN [java_user] WITH PASSWORD = N'P@ssw0rd123';
GO
USE [SanBongBTL];
GO
CREATE USER [java_user] FOR LOGIN [java_user];
GO
ALTER ROLE [db_datareader] ADD MEMBER [java_user];
ALTER ROLE [db_datawriter] ADD MEMBER [java_user];
GO

-- ============================================================
-- SeedData_Full.sql
-- Thêm dữ liệu mẫu đầy đủ cho tất cả các bảng (nếu chưa có)
-- Chạy sau khi đã tạo database và các bảng.
-- ============================================================

USE SanBongBTL;
GO

-- Hàm kiểm tra tồn tại bản ghi (dùng NOT EXISTS để tránh duplicate)
-- Chúng ta sẽ dùng IF NOT EXISTS ... INSERT cho từng bảng.

-- ============================================================
-- 1. VungKhuVucs (nếu chưa có)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM VungKhuVucs WHERE TenVung = N'Nội ô trung tâm')
BEGIN
    INSERT INTO VungKhuVucs (TenVung, MoTa, TyLeHoaHong, MauSac, Lat, Lng, DefaultZoom, ThuTu)
    VALUES 
        (N'Nội ô trung tâm', N'Quận Hoàn Kiếm, Ba Đình, Đống Đa, Hai Bà Trưng', 0.10, '#1ed760', 21.0285, 105.8542, 13, 1),
        (N'Nội ô mở rộng', N'Cầu Giấy, Tây Hồ, Thanh Xuân, Bắc Từ Liêm', 0.08, '#6caee0', 21.0500, 105.7900, 12, 2),
        (N'Vùng ngoại ô', N'Hà Đông, Long Biên, Hoàng Mai, Nam Từ Liêm', 0.06, '#f39c12', 20.9800, 105.8800, 11, 3),
        (N'Vùng xa trung tâm', N'Sơn Tây, Ba Vì, Mỹ Đức', 0.05, '#e74c3c', 21.1200, 105.5000, 10, 4);
END
GO

-- ============================================================
-- 2. DanhMucQuan (bổ sung thêm nếu thiếu, gán VungKhuVucId)
-- ============================================================
-- Lấy ID các vùng đã insert
DECLARE @v1 INT = (SELECT Id FROM VungKhuVucs WHERE TenVung = N'Nội ô trung tâm');
DECLARE @v2 INT = (SELECT Id FROM VungKhuVucs WHERE TenVung = N'Nội ô mở rộng');
DECLARE @v3 INT = (SELECT Id FROM VungKhuVucs WHERE TenVung = N'Vùng ngoại ô');
DECLARE @v4 INT = (SELECT Id FROM VungKhuVucs WHERE TenVung = N'Vùng xa trung tâm');

-- Chèn các quận nếu chưa có
MERGE INTO DanhMucQuan AS target
USING (VALUES
    (N'Cầu Giấy',     N'Hà Nội', 1, @v2),
    (N'Đống Đa',      N'Hà Nội', 2, @v1),
    (N'Hoàng Mai',    N'Hà Nội', 3, @v3),
    (N'Long Biên',    N'Hà Nội', 4, @v3),
    (N'Nam Từ Liêm',  N'Hà Nội', 5, @v3),
    (N'Bắc Từ Liêm',  N'Hà Nội', 6, @v2),
    (N'Tây Hồ',       N'Hà Nội', 7, @v2),
    (N'Ba Đình',      N'Hà Nội', 8, @v1),
    (N'Hai Bà Trưng', N'Hà Nội', 9, @v1),
    (N'Thanh Xuân',   N'Hà Nội',10, @v2),
    (N'Hà Đông',      N'Hà Nội',11, @v3),
    (N'Hoàn Kiếm',    N'Hà Nội',12, @v1)
) AS source (TenQuan, ThanhPho, ThuTu, VungKhuVucId)
ON target.TenQuan = source.TenQuan
WHEN NOT MATCHED THEN
    INSERT (TenQuan, ThanhPho, ThuTu, VungKhuVucId, IsActive)
    VALUES (source.TenQuan, source.ThanhPho, source.ThuTu, source.VungKhuVucId, 1);
GO

-- ============================================================
-- 3. DanhMucLoaiSan, DanhMucLoaiCo, DanhMucDichVu (nếu chưa có)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM DanhMucLoaiSan WHERE MaLoai = '5')
    INSERT INTO DanhMucLoaiSan (MaLoai, TenLoai) VALUES ('5', N'Sân 5 người');
IF NOT EXISTS (SELECT 1 FROM DanhMucLoaiSan WHERE MaLoai = '7')
    INSERT INTO DanhMucLoaiSan (MaLoai, TenLoai) VALUES ('7', N'Sân 7 người');
IF NOT EXISTS (SELECT 1 FROM DanhMucLoaiSan WHERE MaLoai = '11')
    INSERT INTO DanhMucLoaiSan (MaLoai, TenLoai) VALUES ('11', N'Sân 11 người');

IF NOT EXISTS (SELECT 1 FROM DanhMucLoaiCo WHERE MaLoai = 'Nhan tao')
    INSERT INTO DanhMucLoaiCo (MaLoai, TenLoai) VALUES ('Nhan tao', N'Cỏ nhân tạo');
IF NOT EXISTS (SELECT 1 FROM DanhMucLoaiCo WHERE MaLoai = 'Tu nhien')
    INSERT INTO DanhMucLoaiCo (MaLoai, TenLoai) VALUES ('Tu nhien', N'Cỏ tự nhiên');

-- Danh mục dịch vụ
MERGE INTO DanhMucDichVu AS target
USING (VALUES
    (N'Nước uống',     N'💧', N'Nước lọc / nước ngọt / tăng lực'),
    (N'Thuê bóng',     N'⚽', N'Bóng thi đấu Size 4/5'),
    (N'Thuê trọng tài',N'🟡', N'Trọng tài có kinh nghiệm'),
    (N'Thuê áo đấu',   N'👕', N'Áo thi đấu có số, 2 màu')
) AS source (TenDichVu, Icon, MoTa)
ON target.TenDichVu = source.TenDichVu
WHEN NOT MATCHED THEN
    INSERT (TenDichVu, Icon, MoTa, IsActive)
    VALUES (source.TenDichVu, source.Icon, source.MoTa, 1);
GO

-- Lấy ID các danh mục dịch vụ
DECLARE @dmNuoc INT = (SELECT Id FROM DanhMucDichVu WHERE TenDichVu = N'Nước uống');
DECLARE @dmBong INT = (SELECT Id FROM DanhMucDichVu WHERE TenDichVu = N'Thuê bóng');
DECLARE @dmTai  INT = (SELECT Id FROM DanhMucDichVu WHERE TenDichVu = N'Thuê trọng tài');
DECLARE @dmAo   INT = (SELECT Id FROM DanhMucDichVu WHERE TenDichVu = N'Thuê áo đấu');

-- ============================================================
-- 4. Users (Admin, Owner, User, Staff) — chỉ thêm nếu chưa tồn tại email
--    Lưu ý: password đã được hash BCrypt (dùng mật khẩu thật: admin123, owner123, user123, staff123)
-- ============================================================
-- Admin
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'admin@pitchhub.vn')
BEGIN
    INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive)
    VALUES (N'Admin PitchHub', 'admin@pitchhub.vn',
            '$2a$11$f0tXD6o7XYAs7/tE0nx4Reiw1.84L2ItgL0tRwE1Bq.GZbE8MMuzS', -- admin123
            '0901000001', 'Admin', 1);
END

-- Owner 1
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'owner1@gmail.com')
BEGIN
    INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive)
    VALUES (N'Vũ Nguyễn Tuấn Kiệt', 'owner1@gmail.com',
            '$2a$11$dZvxGdl0dNQWsvIM4IO2VuM4kGwP60qFmpbIOKvD0iLulA00/4cCW', -- owner123
            '0901000002', 'Owner', 1);
END

-- Owner 2 (thêm Owner thứ hai để test nhiều chủ sân)
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'owner2@gmail.com')
BEGIN
    INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive)
    VALUES (N'Lê Văn Sỹ', 'owner2@gmail.com',
            '$2a$11$dZvxGdl0dNQWsvIM4IO2VuM4kGwP60qFmpbIOKvD0iLulA00/4cCW', -- owner123
            '0901000005', 'Owner', 1);
END

-- User 1
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'user1@gmail.com')
BEGIN
    INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive)
    VALUES (N'Nguyễn Công Nam', 'user1@gmail.com',
            '$2a$11$wmJXgVs5/RBXU2y/vP4Bs.UcgtzPg0r4Iv2t3DgsZvDrOKD32vPzO', -- user123
            '0901000003', 'User', 1);
END

-- User 2 (thêm user thứ hai)
IF NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'user2@gmail.com')
BEGIN
    INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive)
    VALUES (N'Trần Thị Bích', 'user2@gmail.com',
            '$2a$11$wmJXgVs5/RBXU2y/vP4Bs.UcgtzPg0r4Iv2t3DgsZvDrOKD32vPzO', -- user123
            '0901000006', 'User', 1);
END

-- Staff cho Owner1
DECLARE @owner1Id INT = (SELECT Id FROM Users WHERE Email = 'owner1@gmail.com');
IF @owner1Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'staff1@pitchhub.vn')
BEGIN
    INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive, OwnerIdCuaStaff)
    VALUES (N'Đào Việt Toàn', 'staff1@pitchhub.vn',
            '$2a$11$oOUcIEMbESDcI5QECnfcBOzJtTAAsWbRM3ZX7KQhH3XIWWdmtfR1S', -- staff123
            '0901000004', 'Staff', 1, @owner1Id);
END

-- Staff cho Owner2
DECLARE @owner2Id INT = (SELECT Id FROM Users WHERE Email = 'owner2@gmail.com');
IF @owner2Id IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Users WHERE Email = 'staff2@pitchhub.vn')
BEGIN
    INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive, OwnerIdCuaStaff)
    VALUES (N'Phạm Minh Đức', 'staff2@pitchhub.vn',
            '$2a$11$oOUcIEMbESDcI5QECnfcBOzJtTAAsWbRM3ZX7KQhH3XIWWdmtfR1S', -- staff123
            '0901000007', 'Staff', 1, @owner2Id);
END

-- Lấy các ID cần dùng
DECLARE @adminId INT = (SELECT Id FROM Users WHERE Email = 'admin@pitchhub.vn');
DECLARE @userId1 INT = (SELECT Id FROM Users WHERE Email = 'user1@gmail.com');
DECLARE @userId2 INT = (SELECT Id FROM Users WHERE Email = 'user2@gmail.com');
DECLARE @staff1Id INT = (SELECT Id FROM Users WHERE Email = 'staff1@pitchhub.vn');
DECLARE @staff2Id INT = (SELECT Id FROM Users WHERE Email = 'staff2@pitchhub.vn');

-- ============================================================
-- 5. SanBongs (thêm sân cho Owner1 và Owner2)
-- ============================================================
-- Owner1: 5 sân (3 đã duyệt, 1 chờ duyệt, 1 từ chối, 1 ẩn)
-- Owner2: 2 sân (đã duyệt)

-- Insert các sân cho Owner1
IF NOT EXISTS (SELECT 1 FROM SanBongs WHERE TenSan = N'Sân Cầu Giấy Sport')
BEGIN
    INSERT INTO SanBongs (TenSan, DiaChi, Quan, ThanhPho, LoaiSan, LoaiCo, MoTa, DanhGiaTrungBinh, Latitude, Longitude, TrangThaiDuyet, TyLeCoc, IsHidden, OwnerId, DaKyHopDong, NgayKyHopDong, NoiDungHopDong, ThoiGianGiuCho, ThoiGianHuyTruocGioDa, PhanTramHoanCocDungHan, PhanTramHoanCocTreHan)
    VALUES 
        (N'Sân Cầu Giấy Sport', N'12 Xuân Thủy', N'Cầu Giấy', N'Hà Nội', '5', 'Nhan tao', N'Sân cỏ nhân tạo chất lượng cao, đèn chiếu sáng.', 4.5, 21.0362, 105.7826, 'DaDuyet', 0.30, 0, @owner1Id, 1, GETDATE(), N'Hợp đồng điện tử mẫu', 15, 120, 1.00, 0.00),
        (N'Sân Đống Đa Arena', N'45 Tây Sơn', N'Đống Đa', N'Hà Nội', '7', 'Nhan tao', N'Sân 7 người rộng rãi, có mái che.', 4.2, 21.0198, 105.8412, 'DaDuyet', 0.30, 0, @owner1Id, 1, GETDATE(), N'Hợp đồng điện tử mẫu', 15, 120, 1.00, 0.00),
        (N'Sân Hoàng Mai FC', N'78 Giải Phóng', N'Hoàng Mai', N'Hà Nội', '5', 'Tu nhien', N'Sân cỏ tự nhiên thoáng mát.', 3.8, 20.9876, 105.8543, 'DaDuyet', 0.50, 0, @owner1Id, 1, GETDATE(), N'Hợp đồng điện tử mẫu', 20, 90, 0.80, 0.00),
        (N'Sân Long Biên Star', N'23 Nguyễn Văn Cừ', N'Long Biên', N'Hà Nội', '11', 'Nhan tao', N'Sân 11 người tiêu chuẩn FIFA.', 4.7, 21.0465, 105.8923, 'DaDuyet', 0.30, 0, @owner1Id, 1, GETDATE(), N'Hợp đồng điện tử mẫu', 15, 120, 1.00, 0.00),
        (N'Sân Chờ Duyệt', N'99 Test Street', N'Nam Từ Liêm', N'Hà Nội', '5', 'Nhan tao', N'Sân đang chờ Admin phê duyệt.', 0, 21.0100, 105.7500, 'ChoDuyet', 0.30, 0, @owner1Id, 0, NULL, NULL, 15, 120, 1.00, 0.00),
        (N'Sân Bị Từ Chối', N'100 Fail St', N'Ba Đình', N'Hà Nội', '7', 'Tu nhien', N'Sân bị từ chối', 0, 21.0300, 105.8100, 'TuChoi', 0.30, 0, @owner1Id, 0, NULL, NULL, 15, 120, 1.00, 0.00),
        (N'Sân Ẩn (Bảo trì)', N'200 Hidden St', N'Tây Hồ', N'Hà Nội', '7', 'Nhan tao', N'Sân đang bảo trì tạm ẩn', 4.0, 21.0700, 105.8200, 'DaDuyet', 0.30, 1, @owner1Id, 1, GETDATE(), N'Hợp đồng điện tử', 15, 120, 1.00, 0.00);
END

-- Sân cho Owner2
IF NOT EXISTS (SELECT 1 FROM SanBongs WHERE TenSan = N'Sân Gò Vấp FC')
BEGIN
    INSERT INTO SanBongs (TenSan, DiaChi, Quan, ThanhPho, LoaiSan, LoaiCo, MoTa, DanhGiaTrungBinh, Latitude, Longitude, TrangThaiDuyet, TyLeCoc, IsHidden, OwnerId, DaKyHopDong, NgayKyHopDong, NoiDungHopDong, ThoiGianGiuCho, ThoiGianHuyTruocGioDa, PhanTramHoanCocDungHan, PhanTramHoanCocTreHan)
    VALUES 
        (N'Sân Gò Vấp FC', N'221 Quang Trung', N'Bắc Từ Liêm', N'Hà Nội', '11', 'Nhan tao', N'Sân 11 người full size, đèn LED', 4.5, 21.1000, 105.7000, 'DaDuyet', 0.30, 0, @owner2Id, 1, GETDATE(), N'Hợp đồng điện tử', 15, 120, 1.00, 0.00),
        (N'Sân Tân Bình Arena', N'87 Trường Chinh', N'Thanh Xuân', N'Hà Nội', '7', 'Tu nhien', N'Sân cỏ tự nhiên rộng rãi', 4.8, 21.0100, 105.8000, 'DaDuyet', 0.40, 0, @owner2Id, 1, GETDATE(), N'Hợp đồng điện tử', 20, 90, 0.90, 0.10);
END

-- Lấy các ID sân (dùng sau)
DECLARE @san1_1 INT = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Cầu Giấy Sport' AND OwnerId = @owner1Id);
DECLARE @san1_2 INT = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Đống Đa Arena' AND OwnerId = @owner1Id);
DECLARE @san1_3 INT = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Hoàng Mai FC' AND OwnerId = @owner1Id);
DECLARE @san1_4 INT = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Long Biên Star' AND OwnerId = @owner1Id);
DECLARE @san1_5 INT = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Chờ Duyệt' AND OwnerId = @owner1Id);
DECLARE @san1_6 INT = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Bị Từ Chối' AND OwnerId = @owner1Id);
DECLARE @san1_7 INT = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Ẩn (Bảo trì)' AND OwnerId = @owner1Id);
DECLARE @san2_1 INT = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Gò Vấp FC' AND OwnerId = @owner2Id);
DECLARE @san2_2 INT = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Tân Bình Arena' AND OwnerId = @owner2Id);

-- ============================================================
-- 6. StaffSanPhanCong (gán staff cho sân)
-- ============================================================
-- Staff1 (của Owner1) gán cho các sân của Owner1
IF NOT EXISTS (SELECT 1 FROM StaffSanPhanCong WHERE StaffId = @staff1Id AND SanBongId = @san1_1)
    INSERT INTO StaffSanPhanCong (StaffId, SanBongId) VALUES (@staff1Id, @san1_1);
IF NOT EXISTS (SELECT 1 FROM StaffSanPhanCong WHERE StaffId = @staff1Id AND SanBongId = @san1_2)
    INSERT INTO StaffSanPhanCong (StaffId, SanBongId) VALUES (@staff1Id, @san1_2);
IF NOT EXISTS (SELECT 1 FROM StaffSanPhanCong WHERE StaffId = @staff1Id AND SanBongId = @san1_3)
    INSERT INTO StaffSanPhanCong (StaffId, SanBongId) VALUES (@staff1Id, @san1_3);

-- Staff2 (của Owner2) gán cho sân của Owner2
IF NOT EXISTS (SELECT 1 FROM StaffSanPhanCong WHERE StaffId = @staff2Id AND SanBongId = @san2_1)
    INSERT INTO StaffSanPhanCong (StaffId, SanBongId) VALUES (@staff2Id, @san2_1);
IF NOT EXISTS (SELECT 1 FROM StaffSanPhanCong WHERE StaffId = @staff2Id AND SanBongId = @san2_2)
    INSERT INTO StaffSanPhanCong (StaffId, SanBongId) VALUES (@staff2Id, @san2_2);

-- ============================================================
-- 7. KhungGios (thêm khung giờ cho các sân)
-- ============================================================
-- Hàm kiểm tra khung giờ đã tồn tại
-- Chỉ thêm nếu chưa có (dùng IF NOT EXISTS)
-- Sân Cầu Giấy Sport (@san1_1)
IF NOT EXISTS (SELECT 1 FROM KhungGios WHERE SanBongId = @san1_1 AND GioBatDau = '06:00' AND LoaiNgay = 'TatCa')
BEGIN
    INSERT INTO KhungGios (SanBongId, GioBatDau, GioKetThuc, Gia, GiaGioVang, GiaCuoiTuan, LoaiNgay, TrangThai) VALUES
        (@san1_1, '06:00', '07:30', 180000, 220000, 200000, 'TatCa', 'Trong'),
        (@san1_1, '07:30', '09:00', 180000, 220000, 200000, 'TatCa', 'Trong'),
        (@san1_1, '09:00', '10:30', 160000, 200000, 180000, 'TatCa', 'Trong'),
        (@san1_1, '15:00', '16:30', 160000, 200000, 180000, 'TatCa', 'Trong'),
        (@san1_1, '16:30', '18:00', 200000, 260000, 240000, 'TatCa', 'Trong'),
        (@san1_1, '18:00', '19:30', 250000, 300000, 320000, 'TatCa', 'Trong'),
        (@san1_1, '19:30', '21:00', 250000, 300000, 320000, 'TatCa', 'Trong');
END

-- Sân Đống Đa Arena (@san1_2)
IF NOT EXISTS (SELECT 1 FROM KhungGios WHERE SanBongId = @san1_2 AND GioBatDau = '06:00')
BEGIN
    INSERT INTO KhungGios (SanBongId, GioBatDau, GioKetThuc, Gia, GiaGioVang, GiaCuoiTuan, LoaiNgay, TrangThai) VALUES
        (@san1_2, '06:00', '07:30', 250000, 300000, 280000, 'TatCa', 'Trong'),
        (@san1_2, '07:30', '09:00', 250000, 300000, 280000, 'TatCa', 'Trong'),
        (@san1_2, '17:00', '18:30', 320000, 380000, 400000, 'TatCa', 'Trong'),
        (@san1_2, '18:30', '20:00', 350000, 420000, 440000, 'TatCa', 'Trong'),
        (@san1_2, '20:00', '21:30', 350000, 420000, 440000, 'TatCa', 'Trong');
END

-- Sân Hoàng Mai FC (@san1_3)
IF NOT EXISTS (SELECT 1 FROM KhungGios WHERE SanBongId = @san1_3 AND GioBatDau = '05:30')
BEGIN
    INSERT INTO KhungGios (SanBongId, GioBatDau, GioKetThuc, Gia, GiaGioVang, GiaCuoiTuan, LoaiNgay, TrangThai) VALUES
        (@san1_3, '05:30', '07:00', 150000, 180000, 170000, 'TatCa', 'Trong'),
        (@san1_3, '07:00', '08:30', 150000, 180000, 170000, 'TatCa', 'Trong'),
        (@san1_3, '16:00', '17:30', 180000, 220000, 200000, 'TatCa', 'Trong'),
        (@san1_3, '17:30', '19:00', 200000, 250000, 230000, 'TatCa', 'Trong');
END

-- Sân Long Biên Star (@san1_4)
IF NOT EXISTS (SELECT 1 FROM KhungGios WHERE SanBongId = @san1_4 AND GioBatDau = '06:00')
BEGIN
    INSERT INTO KhungGios (SanBongId, GioBatDau, GioKetThuc, Gia, GiaGioVang, GiaCuoiTuan, LoaiNgay, TrangThai) VALUES
        (@san1_4, '06:00', '08:00', 500000, 600000, 580000, 'TatCa', 'Trong'),
        (@san1_4, '08:00', '10:00', 500000, 600000, 580000, 'TatCa', 'Trong'),
        (@san1_4, '15:00', '17:00', 550000, 650000, 620000, 'TatCa', 'Trong'),
        (@san1_4, '17:00', '19:00', 650000, 780000, 750000, 'TatCa', 'Trong'),
        (@san1_4, '19:00', '21:00', 650000, 780000, 750000, 'TatCa', 'Trong');
END

-- Sân Gò Vấp FC (@san2_1) - thêm vài khung giờ
IF NOT EXISTS (SELECT 1 FROM KhungGios WHERE SanBongId = @san2_1 AND GioBatDau = '06:00')
BEGIN
    INSERT INTO KhungGios (SanBongId, GioBatDau, GioKetThuc, Gia, GiaGioVang, GiaCuoiTuan, LoaiNgay, TrangThai) VALUES
        (@san2_1, '06:00', '08:00', 400000, 500000, 480000, 'TatCa', 'Trong'),
        (@san2_1, '08:00', '10:00', 400000, 500000, 480000, 'TatCa', 'Trong'),
        (@san2_1, '16:00', '18:00', 500000, 600000, 550000, 'TatCa', 'Trong'),
        (@san2_1, '18:00', '20:00', 600000, 700000, 650000, 'TatCa', 'Trong'),
        (@san2_1, '20:00', '22:00', 600000, 700000, 650000, 'TatCa', 'Trong');
END

-- Sân Tân Bình Arena (@san2_2)
IF NOT EXISTS (SELECT 1 FROM KhungGios WHERE SanBongId = @san2_2 AND GioBatDau = '06:00')
BEGIN
    INSERT INTO KhungGios (SanBongId, GioBatDau, GioKetThuc, Gia, GiaGioVang, GiaCuoiTuan, LoaiNgay, TrangThai) VALUES
        (@san2_2, '06:00', '07:30', 180000, 220000, 200000, 'ThuThuong', 'Trong'),
        (@san2_2, '07:30', '09:00', 180000, 220000, 200000, 'ThuThuong', 'Trong'),
        (@san2_2, '17:00', '18:30', 250000, 300000, 280000, 'ThuThuong', 'Trong'),
        (@san2_2, '18:30', '20:00', 280000, 350000, 320000, 'ThuThuong', 'Trong');
END

-- Lấy một số khung giờ để đặt sân (đã có sẵn từ data cũ)
DECLARE @kgSan1_1_1 INT = (SELECT Id FROM KhungGios WHERE SanBongId = @san1_1 AND GioBatDau = '18:00' AND LoaiNgay = 'TatCa');
DECLARE @kgSan1_2_1 INT = (SELECT Id FROM KhungGios WHERE SanBongId = @san1_2 AND GioBatDau = '17:00' AND LoaiNgay = 'TatCa');
DECLARE @kgSan2_1_1 INT = (SELECT Id FROM KhungGios WHERE SanBongId = @san2_1 AND GioBatDau = '18:00' AND LoaiNgay = 'TatCa');

-- ============================================================
-- 8. DichVus (dịch vụ cho từng sân)
-- ============================================================
-- Thêm dịch vụ cho sân Cầu Giấy Sport (@san1_1)
IF NOT EXISTS (SELECT 1 FROM DichVus WHERE SanBongId = @san1_1 AND DanhMucDichVuId = @dmNuoc)
    INSERT INTO DichVus (SanBongId, DanhMucDichVuId, TenDichVu, Gia, TonKho, IsActive) VALUES (@san1_1, @dmNuoc, N'Nước uống', 15000, 100, 1);
IF NOT EXISTS (SELECT 1 FROM DichVus WHERE SanBongId = @san1_1 AND DanhMucDichVuId = @dmBong)
    INSERT INTO DichVus (SanBongId, DanhMucDichVuId, TenDichVu, Gia, TonKho, IsActive) VALUES (@san1_1, @dmBong, N'Thuê bóng', 30000, 15, 1);
IF NOT EXISTS (SELECT 1 FROM DichVus WHERE SanBongId = @san1_1 AND DanhMucDichVuId = @dmTai)
    INSERT INTO DichVus (SanBongId, DanhMucDichVuId, TenDichVu, Gia, TonKho, IsActive) VALUES (@san1_1, @dmTai, N'Thuê trọng tài', 100000, 5, 1);
IF NOT EXISTS (SELECT 1 FROM DichVus WHERE SanBongId = @san1_1 AND DanhMucDichVuId = @dmAo)
    INSERT INTO DichVus (SanBongId, DanhMucDichVuId, TenDichVu, Gia, TonKho, IsActive) VALUES (@san1_1, @dmAo, N'Thuê áo đấu', 20000, 20, 1);

-- Tương tự cho sân Đống Đa Arena
IF NOT EXISTS (SELECT 1 FROM DichVus WHERE SanBongId = @san1_2 AND DanhMucDichVuId = @dmNuoc)
    INSERT INTO DichVus (SanBongId, DanhMucDichVuId, TenDichVu, Gia, TonKho, IsActive) VALUES (@san1_2, @dmNuoc, N'Nước uống', 15000, 80, 1);
IF NOT EXISTS (SELECT 1 FROM DichVus WHERE SanBongId = @san1_2 AND DanhMucDichVuId = @dmBong)
    INSERT INTO DichVus (SanBongId, DanhMucDichVuId, TenDichVu, Gia, TonKho, IsActive) VALUES (@san1_2, @dmBong, N'Thuê bóng', 30000, 10, 1);

-- Sân Gò Vấp FC
IF NOT EXISTS (SELECT 1 FROM DichVus WHERE SanBongId = @san2_1 AND DanhMucDichVuId = @dmNuoc)
    INSERT INTO DichVus (SanBongId, DanhMucDichVuId, TenDichVu, Gia, TonKho, IsActive) VALUES (@san2_1, @dmNuoc, N'Nước uống', 20000, 200, 1);
IF NOT EXISTS (SELECT 1 FROM DichVus WHERE SanBongId = @san2_1 AND DanhMucDichVuId = @dmBong)
    INSERT INTO DichVus (SanBongId, DanhMucDichVuId, TenDichVu, Gia, TonKho, IsActive) VALUES (@san2_1, @dmBong, N'Thuê bóng', 40000, 25, 1);
IF NOT EXISTS (SELECT 1 FROM DichVus WHERE SanBongId = @san2_1 AND DanhMucDichVuId = @dmTai)
    INSERT INTO DichVus (SanBongId, DanhMucDichVuId, TenDichVu, Gia, TonKho, IsActive) VALUES (@san2_1, @dmTai, N'Thuê trọng tài', 150000, 4, 1);
IF NOT EXISTS (SELECT 1 FROM DichVus WHERE SanBongId = @san2_1 AND DanhMucDichVuId = @dmAo)
    INSERT INTO DichVus (SanBongId, DanhMucDichVuId, TenDichVu, Gia, TonKho, IsActive) VALUES (@san2_1, @dmAo, N'Thuê áo đấu', 25000, 30, 1);

-- Lấy ID các dịch vụ (dùng cho DatSan_DichVus)
DECLARE @dv1_nuoc INT = (SELECT Id FROM DichVus WHERE SanBongId = @san1_1 AND DanhMucDichVuId = @dmNuoc);
DECLARE @dv1_bong INT = (SELECT Id FROM DichVus WHERE SanBongId = @san1_1 AND DanhMucDichVuId = @dmBong);
DECLARE @dv1_tai  INT = (SELECT Id FROM DichVus WHERE SanBongId = @san1_1 AND DanhMucDichVuId = @dmTai);

-- ============================================================
-- 9. DatSans (đơn đặt sân) — nhiều trạng thái khác nhau
-- ============================================================
-- 9.1. Đơn đã xác nhận (DaXacNhan) — ngày đá trong tương lai gần
IF NOT EXISTS (SELECT 1 FROM DatSans WHERE MaXacNhan = 'ORDER001')
BEGIN
    INSERT INTO DatSans (UserId, KhungGioId, NgayThiDau, TienCoc, TongTien, MaXacNhan, TrangThai, ThoiGianTao)
    VALUES (@userId1, @kgSan1_1_1, DATEADD(DAY, 2, GETDATE()), 75000, 250000, 'ORDER001', 'DaXacNhan', GETDATE());
END

-- 9.2. Đơn chờ xác nhận (ChoDuyet)
IF NOT EXISTS (SELECT 1 FROM DatSans WHERE MaXacNhan = 'ORDER002')
BEGIN
    INSERT INTO DatSans (UserId, KhungGioId, NgayThiDau, TienCoc, TongTien, MaXacNhan, TrangThai, ThoiGianTao)
    VALUES (@userId2, @kgSan1_2_1, DATEADD(DAY, 3, GETDATE()), 96000, 0, 'ORDER002', 'ChoDuyet', GETDATE());
END

-- 9.3. Đơn đang sử dụng (DangSuDung) — ngày đá là hôm nay, có check-in
IF NOT EXISTS (SELECT 1 FROM DatSans WHERE MaXacNhan = 'ORDER003')
BEGIN
    INSERT INTO DatSans (UserId, KhungGioId, NgayThiDau, TienCoc, TongTien, MaXacNhan, TrangThai, StaffCheckInId, ThoiGianTao)
    VALUES (@userId1, @kgSan2_1_1, GETDATE(), 120000, 600000, 'ORDER003', 'DangSuDung', @staff2Id, GETDATE());
END

-- 9.4. Đơn hoàn thành (HoanThanh) — đã check-out, có tổng tiền
IF NOT EXISTS (SELECT 1 FROM DatSans WHERE MaXacNhan = 'ORDER004')
BEGIN
    INSERT INTO DatSans (UserId, KhungGioId, NgayThiDau, TienCoc, TongTien, MaXacNhan, TrangThai, StaffCheckInId, StaffCheckOutId, ThoiGianTao)
    VALUES (@userId2, @kgSan1_1_1, DATEADD(DAY, -5, GETDATE()), 75000, 250000, 'ORDER004', 'HoanThanh', @staff1Id, @staff1Id, DATEADD(DAY, -5, GETDATE()));
END

-- 9.5. Đơn đã hủy (DaHuy) — có ghi chú lý do
IF NOT EXISTS (SELECT 1 FROM DatSans WHERE MaXacNhan = 'ORDER005')
BEGIN
    INSERT INTO DatSans (UserId, KhungGioId, NgayThiDau, TienCoc, TongTien, MaXacNhan, TrangThai, GhiChuSuCo, ThoiGianTao)
    VALUES (@userId1, @kgSan1_2_1, DATEADD(DAY, 10, GETDATE()), 96000, 0, 'ORDER005', 'DaHuy', N'Khách hủy do mưa bão', GETDATE());
END

-- Lấy ID các đơn đặt
DECLARE @order1 INT = (SELECT Id FROM DatSans WHERE MaXacNhan = 'ORDER001');
DECLARE @order2 INT = (SELECT Id FROM DatSans WHERE MaXacNhan = 'ORDER002');
DECLARE @order3 INT = (SELECT Id FROM DatSans WHERE MaXacNhan = 'ORDER003');
DECLARE @order4 INT = (SELECT Id FROM DatSans WHERE MaXacNhan = 'ORDER004');
DECLARE @order5 INT = (SELECT Id FROM DatSans WHERE MaXacNhan = 'ORDER005');

-- ============================================================
-- 10. DatSan_DichVus (dịch vụ kèm theo đơn)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM DatSan_DichVus WHERE DatSanId = @order1 AND DichVuId = @dv1_nuoc)
    INSERT INTO DatSan_DichVus (DatSanId, DichVuId, SoLuong) VALUES (@order1, @dv1_nuoc, 5);
IF NOT EXISTS (SELECT 1 FROM DatSan_DichVus WHERE DatSanId = @order1 AND DichVuId = @dv1_bong)
    INSERT INTO DatSan_DichVus (DatSanId, DichVuId, SoLuong) VALUES (@order1, @dv1_bong, 2);
IF NOT EXISTS (SELECT 1 FROM DatSan_DichVus WHERE DatSanId = @order4 AND DichVuId = @dv1_tai)
    INSERT INTO DatSan_DichVus (DatSanId, DichVuId, SoLuong) VALUES (@order4, @dv1_tai, 1);

-- ============================================================
-- 11. DanhGias (đánh giá) — chỉ cho đơn hoàn thành
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM DanhGias WHERE DatSanId = @order4)
    INSERT INTO DanhGias (SanBongId, UserId, DatSanId, SoSao, NhanXet, NgayDanhGia)
    VALUES (@san1_1, @userId2, @order4, 5, N'Sân rất đẹp, dịch vụ tốt, sẽ quay lại!', GETDATE());

-- Thêm đánh giá khác cho sân khác (giả sử có đơn hoàn thành khác)
-- Ở đây thêm trực tiếp không cần đơn (demo)
IF NOT EXISTS (SELECT 1 FROM DanhGias WHERE SanBongId = @san2_1 AND UserId = @userId1)
    INSERT INTO DanhGias (SanBongId, UserId, DatSanId, SoSao, NhanXet, NgayDanhGia)
    VALUES (@san2_1, @userId1, @order3, 4, N'Sân rộng, đèn sáng, nhưng hơi xa', GETDATE());

-- Cập nhật điểm trung bình cho các sân (đã có sẵn từ dữ liệu mẫu, nhưng update lại theo đánh giá thực)
UPDATE SanBongs SET DanhGiaTrungBinh = (SELECT AVG(SoSao) FROM DanhGias WHERE SanBongId = @san1_1) WHERE Id = @san1_1;
UPDATE SanBongs SET DanhGiaTrungBinh = 4.5 WHERE Id = @san2_1;

-- ============================================================
-- 12. Matchmakings (tìm đối thủ) — cho đơn đã xác nhận hoặc đang chờ
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Matchmakings WHERE DatSanId = @order1)
    INSERT INTO Matchmakings (DatSanId, UserId, TieuDe, MoTa, SoNguoiCanThem, TrangThai, NgayDang)
    VALUES (@order1, @userId1, N'Cần thêm 2 cầu thủ', N'Đội thiếu 2 tiền đạo, trình độ khá', 2, 'DangTim', GETDATE());

IF NOT EXISTS (SELECT 1 FROM Matchmakings WHERE DatSanId = @order2)
    INSERT INTO Matchmakings (DatSanId, UserId, TieuDe, MoTa, SoNguoiCanThem, TrangThai, NgayDang)
    VALUES (@order2, @userId2, N'Tìm đội giao lưu', N'Nhóm 5 người, muốn đá giao hữu', 5, 'DangTim', GETDATE());

-- ============================================================
-- 13. KhieuNais (khiếu nại) — cho đơn đã hủy hoặc hoàn thành nhưng có vấn đề
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM KhieuNais WHERE DatSanId = @order5)
    INSERT INTO KhieuNais (DatSanId, UserId, LyDo, TrangThai, NgayGui)
    VALUES (@order5, @userId1, N'Sân không đúng như mô tả, cỏ xấu, đèn không sáng', 'ChoXuLy', GETDATE());

-- ============================================================
-- 14. AuditLogs (ghi log hành động)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM AuditLogs WHERE HanhDong = 'PheDuyetSan' AND DoiTuongId = @san1_1)
    INSERT INTO AuditLogs (UserId, VaiTro, HanhDong, DoiTuong, DoiTuongId, MoTa, ThoiGian)
    VALUES (@adminId, 'Admin', 'PheDuyetSan', 'SanBong', @san1_1, N'Admin phê duyệt sân Cầu Giấy Sport', GETDATE());

IF NOT EXISTS (SELECT 1 FROM AuditLogs WHERE HanhDong = 'DangNhap' AND UserId = @userId1)
    INSERT INTO AuditLogs (UserId, VaiTro, HanhDong, DoiTuong, DoiTuongId, MoTa, ThoiGian)
    VALUES (@userId1, 'User', 'DangNhap', 'Login', @userId1, N'Người dùng đăng nhập thành công', GETDATE());

-- ============================================================
-- 15. AnhSanBongs (ảnh sân)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM AnhSanBongs WHERE SanBongId = @san1_1 AND ThuTu = 1)
    INSERT INTO AnhSanBongs (SanBongId, DuongDan, LoaiAnh, ThuTu, MoTa) VALUES (@san1_1, 'https://example.com/san1_1.jpg', 'URL', 1, N'Ảnh sân chính');
IF NOT EXISTS (SELECT 1 FROM AnhSanBongs WHERE SanBongId = @san1_2 AND ThuTu = 1)
    INSERT INTO AnhSanBongs (SanBongId, DuongDan, LoaiAnh, ThuTu, MoTa) VALUES (@san1_2, 'https://example.com/san1_2.jpg', 'URL', 1, N'Ảnh sân Đống Đa');
IF NOT EXISTS (SELECT 1 FROM AnhSanBongs WHERE SanBongId = @san2_1 AND ThuTu = 1)
    INSERT INTO AnhSanBongs (SanBongId, DuongDan, LoaiAnh, ThuTu, MoTa) VALUES (@san2_1, 'https://example.com/san2_1.jpg', 'URL', 1, N'Sân Gò Vấp');

-- ============================================================
-- Kết thúc seed data
-- ============================================================
PRINT '================================================';
PRINT N'✅ Seed data hoàn tất! Dữ liệu test đã được thêm.';
PRINT '================================================';
GO