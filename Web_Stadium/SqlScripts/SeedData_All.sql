-- ============================================================
--  PitchHub.vn — SEED DATA cho TẤT CẢ các bảng (10–15 bản ghi/bảng)
--  Tương thích schema: V3 (core) + V5 (Voucher/OTP/...) + V6 (Giải đấu)
--                      + UC068–UC071 (Owner ops)
--
--  YÊU CẦU:
--    1. Database 'SanBongBTL' đã tồn tại và đã áp dụng đầy đủ migration.
--    2. Chạy 1 lần. Nếu muốn chạy lại, bỏ comment khối "BƯỚC 0: RESET".
--
--  GHI CHÚ MẬT KHẨU (đã verify hoạt động với BCrypt.Net-Next):
--    Admin  → admin123
--    Owner  → owner123
--    Staff  → staff123
--    User   → user123
--  Nếu muốn đổi: Console.WriteLine(BCrypt.Net.BCrypt.HashPassword("new_pw"))
--  rồi thay vào biến @PwdAdmin/@PwdOwner/@PwdStaff/@PwdUser bên dưới.
-- ============================================================

USE SanBongBTL;
GO

SET NOCOUNT ON;
SET XACT_ABORT ON;
GO

BEGIN TRANSACTION;
GO

-- ============================================================
-- BƯỚC 0: RESET (tùy chọn — mặc định KHÔNG xóa)
-- Bỏ comment cả khối khi muốn seed lại từ đầu
-- ============================================================

DELETE FROM SuKienTrans;
DELETE FROM TranDaus;
DELETE FROM ThanhVienDois;
DELETE FROM DoiBongs;
DELETE FROM BangDaus;
DELETE FROM GiaiDaus;
DELETE FROM ChuyenNhuongDatSans;
DELETE FROM YeuCauDoiSans;
DELETE FROM YeuCauDoiGios;
DELETE FROM GiaoDichHoanCocs;
DELETE FROM OtpCodes;
DELETE FROM DiemThuongLogs;
DELETE FROM SanYeuThichs;
DELETE FROM UserVouchers;
DELETE FROM Vouchers;
DELETE FROM AuditLogs;
DELETE FROM KhieuNais;
DELETE FROM Matchmakings;
DELETE FROM DanhGias;
DELETE FROM DatSan_DichVus;
DELETE FROM DatSans;
DELETE FROM DichVus;
DELETE FROM AnhSanBongs;
DELETE FROM StaffSanPhanCong;
DELETE FROM KhungGios;
DELETE FROM SanBongs;
DELETE FROM Users;
DELETE FROM DanhMucDichVu;
DELETE FROM DanhMucLoaiCo;
DELETE FROM DanhMucLoaiSan;
DELETE FROM DanhMucQuan;
DELETE FROM VungKhuVucs;
DBCC CHECKIDENT('VungKhuVucs',     RESEED, 0);
DBCC CHECKIDENT('DanhMucQuan',     RESEED, 0);
DBCC CHECKIDENT('DanhMucLoaiSan',  RESEED, 0);
DBCC CHECKIDENT('DanhMucLoaiCo',   RESEED, 0);
DBCC CHECKIDENT('DanhMucDichVu',   RESEED, 0);
DBCC CHECKIDENT('Users',           RESEED, 0);
DBCC CHECKIDENT('SanBongs',        RESEED, 0);
DBCC CHECKIDENT('KhungGios',       RESEED, 0);
DBCC CHECKIDENT('AnhSanBongs',     RESEED, 0);
DBCC CHECKIDENT('DichVus',         RESEED, 0);
DBCC CHECKIDENT('DatSans',         RESEED, 0);


-- ============================================================
-- BƯỚC 1: VungKhuVucs (10 vùng)
-- ============================================================
INSERT INTO VungKhuVucs (TenVung, MoTa, TyLeHoaHong, MauSac, Lat, Lng, DefaultZoom, ThuTu, IsActive)
VALUES
    (N'Trung tâm Hà Nội',  N'Khu vực nội thành lõi',           0.10, '#1ed760', 21.0285, 105.8542, 12,  1, 1),
    (N'Tây Hà Nội',        N'Khu vực phía Tây Thủ đô',          0.10, '#3b82f6', 21.0331, 105.7796, 12,  2, 1),
    (N'Đông Hà Nội',       N'Khu vực phía Đông sông Hồng',      0.10, '#f59e0b', 21.0410, 105.8990, 12,  3, 1),
    (N'Nam Hà Nội',        N'Khu vực phía Nam thủ đô',          0.10, '#ef4444', 20.9876, 105.8543, 12,  4, 1),
    (N'Bắc Hà Nội',        N'Khu vực phía Bắc thủ đô',          0.10, '#8b5cf6', 21.0700, 105.8400, 12,  5, 1),
    (N'TP. Hồ Chí Minh',   N'Khu vực Sài Gòn',                  0.12, '#06b6d4', 10.7769, 106.7009, 11,  6, 1),
    (N'Hải Phòng',         N'Thành phố cảng Hải Phòng',         0.08, '#22c55e', 20.8449, 106.6881, 12,  7, 1),
    (N'Đà Nẵng',           N'Thành phố biển Đà Nẵng',           0.10, '#facc15', 16.0544, 108.2022, 12,  8, 1),
    (N'Cần Thơ',           N'Khu vực miền Tây',                 0.08, '#ec4899', 10.0452, 105.7469, 12,  9, 1),
    (N'Bình Dương',        N'Tỉnh Bình Dương',                  0.10, '#10b981', 11.3254, 106.4770, 12, 10, 1);
PRINT N'✔ VungKhuVucs: 10 vùng';

DECLARE @VungTTHN INT = (SELECT Id FROM VungKhuVucs WHERE TenVung = N'Trung tâm Hà Nội');
DECLARE @VungTayHN INT = (SELECT Id FROM VungKhuVucs WHERE TenVung = N'Tây Hà Nội');
DECLARE @VungDongHN INT = (SELECT Id FROM VungKhuVucs WHERE TenVung = N'Đông Hà Nội');
DECLARE @VungNamHN INT = (SELECT Id FROM VungKhuVucs WHERE TenVung = N'Nam Hà Nội');
DECLARE @VungBacHN INT = (SELECT Id FROM VungKhuVucs WHERE TenVung = N'Bắc Hà Nội');

-- ============================================================
-- BƯỚC 2: DanhMucQuan (12 quận)
-- ============================================================
INSERT INTO DanhMucQuan (TenQuan, ThanhPho, ThuTu, IsActive, VungKhuVucId)
VALUES
    (N'Cầu Giấy',     N'Hà Nội',  1, 1, @VungTayHN),
    (N'Đống Đa',      N'Hà Nội',  2, 1, @VungTTHN),
    (N'Hoàng Mai',    N'Hà Nội',  3, 1, @VungNamHN),
    (N'Long Biên',    N'Hà Nội',  4, 1, @VungDongHN),
    (N'Nam Từ Liêm',  N'Hà Nội',  5, 1, @VungTayHN),
    (N'Bắc Từ Liêm',  N'Hà Nội',  6, 1, @VungBacHN),
    (N'Tây Hồ',       N'Hà Nội',  7, 1, @VungTTHN),
    (N'Ba Đình',      N'Hà Nội',  8, 1, @VungTTHN),
    (N'Hai Bà Trưng', N'Hà Nội',  9, 1, @VungTTHN),
    (N'Thanh Xuân',   N'Hà Nội', 10, 1, @VungTayHN),
    (N'Hà Đông',      N'Hà Nội', 11, 1, @VungTayHN),
    (N'Hoàn Kiếm',    N'Hà Nội', 12, 1, @VungTTHN);
PRINT N'✔ DanhMucQuan: 12 quận';

-- ============================================================
-- BƯỚC 3: DanhMucLoaiSan (10 loại)
-- ============================================================
INSERT INTO DanhMucLoaiSan (MaLoai, TenLoai, IsActive)
VALUES
    ('3',  N'Sân 3 người (Futsal mini)', 1),
    ('4',  N'Sân 4 người',               1),
    ('5',  N'Sân 5 người',               1),
    ('6',  N'Sân 6 người',               1),
    ('7',  N'Sân 7 người',               1),
    ('8',  N'Sân 8 người',               1),
    ('9',  N'Sân 9 người',               1),
    ('10', N'Sân 10 người',              1),
    ('11', N'Sân 11 người',              1),
    ('F5', N'Futsal trong nhà (5 người)',1);
PRINT N'✔ DanhMucLoaiSan: 10 loại';

-- ============================================================
-- BƯỚC 4: DanhMucLoaiCo (10 loại)
-- ============================================================
INSERT INTO DanhMucLoaiCo (MaLoai, TenLoai, IsActive)
VALUES
    ('Nhan tao',     N'Cỏ nhân tạo tiêu chuẩn',          1),
    ('Tu nhien',     N'Cỏ tự nhiên',                      1),
    ('Hybrid',       N'Cỏ Hybrid (kết hợp)',              1),
    ('Nhan tao 5cm', N'Cỏ nhân tạo sợi dài 5cm',          1),
    ('Nhan tao 6cm', N'Cỏ nhân tạo sợi dài 6cm',          1),
    ('Nhan tao 4cm', N'Cỏ nhân tạo sợi ngắn 4cm',         1),
    ('Trong nha',    N'Sàn gỗ trong nhà',                 1),
    ('Be tong',      N'Bê tông phủ thảm',                 1),
    ('Da nhao',      N'Cỏ tự nhiên thoáng đãi (mùa khô)', 1),
    ('Tham nhan',    N'Thảm nhân tạo trải nhà thi đấu',   1);
PRINT N'✔ DanhMucLoaiCo: 10 loại';

-- ============================================================
-- BƯỚC 5: DanhMucDichVu (12 danh mục)
-- ============================================================
INSERT INTO DanhMucDichVu (TenDichVu, Icon, MoTa, IsActive)
VALUES
    (N'Nước uống',        N'💧', N'Nước lọc / nước ngọt / tăng lực',         1),
    (N'Thuê bóng',        N'⚽', N'Bóng thi đấu tiêu chuẩn Size 4/5',         1),
    (N'Thuê trọng tài',   N'🟡', N'Trọng tài có kinh nghiệm',                 1),
    (N'Thuê áo đấu',      N'👕', N'Áo thi đấu có số, 2 màu',                  1),
    (N'Thuê giày',        N'👟', N'Giày đinh thi đấu',                        1),
    (N'Quay phim',        N'🎥', N'Quay phim trận đấu, livestream',           1),
    (N'Massage hồi phục', N'💆', N'Dịch vụ massage sau trận',                 1),
    (N'Cho thuê băng đội',N'🟢', N'Băng đội trưởng',                          1),
    (N'Đồ ăn nhẹ',        N'🍔', N'Bánh mì, snack, mì gói',                   1),
    (N'Thuê tất',         N'🧦', N'Tất thi đấu',                              1),
    (N'Xe đón sân',       N'🚌', N'Xe đưa đón tới sân (khu vực HN)',          1),
    (N'In ảnh trận đấu',  N'🖼️',N'In ảnh kỷ niệm trận đấu',                   0);  -- 1 record không active để có biến thể
PRINT N'✔ DanhMucDichVu: 12 danh mục';

-- ============================================================
-- BƯỚC 6: Users (15 — 1 Admin, 3 Owner, 3 Staff, 8 User)
-- Hash BCrypt thực, đã verify với BCrypt.Net-Next:
--   Admin → admin123 | Owner → owner123 | Staff → staff123 | User → user123
-- ============================================================
DECLARE @PwdAdmin NVARCHAR(255) = '$2a$11$f0tXD6o7XYAs7/tE0nx4Reiw1.84L2ItgL0tRwE1Bq.GZbE8MMuzS';   -- admin123
DECLARE @PwdOwner NVARCHAR(255) = '$2a$11$dZvxGdl0dNQWsvIM4IO2VuM4kGwP60qFmpbIOKvD0iLulA00/4cCW';   -- owner123
DECLARE @PwdStaff NVARCHAR(255) = '$2a$11$oOUcIEMbESDcI5QECnfcBOzJtTAAsWbRM3ZX7KQhH3XIWWdmtfR1S';   -- staff123
DECLARE @PwdUser  NVARCHAR(255) = '$2a$11$wmJXgVs5/RBXU2y/vP4Bs.UcgtzPg0r4Iv2t3DgsZvDrOKD32vPzO';   -- user123

INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive, NgayTao,
                   DaXacThucSdt, DiemHienTai, NganHang, SoTaiKhoan, TenTaiKhoan)
VALUES
    -- Admin
    (N'Admin PitchHub',       'admin@pitchhub.vn', @PwdAdmin, '0901000001', 'Admin', 1, GETDATE(), 1, 0,    NULL, NULL, NULL),
    -- Owners
    (N'Vũ Nguyễn Tuấn Kiệt',  'owner1@gmail.com',  @PwdOwner, '0901000002', 'Owner', 1, GETDATE(), 1, 0,    N'Vietcombank', '0123456789', N'VU NGUYEN TUAN KIET'),
    (N'Lê Hoàng Anh',         'owner2@gmail.com',  @PwdOwner, '0901000003', 'Owner', 1, GETDATE(), 1, 0,    N'Techcombank', '1100990011', N'LE HOANG ANH'),
    (N'Phạm Thị Mai',         'owner3@gmail.com',  @PwdOwner, '0901000004', 'Owner', 1, GETDATE(), 1, 0,    N'BIDV',        '2200110022', N'PHAM THI MAI'),
    -- Users (8)
    (N'Nguyễn Công Nam',      'user1@gmail.com',   @PwdUser,  '0901000005', 'User',  1, GETDATE(), 1, 1200, N'MB Bank',     '0011001100', N'NGUYEN CONG NAM'),
    (N'Trần Quốc Bảo',        'user2@gmail.com',   @PwdUser,  '0901000006', 'User',  1, GETDATE(), 1, 800,  NULL, NULL, NULL),
    (N'Đỗ Minh Quân',         'user3@gmail.com',   @PwdUser,  '0901000007', 'User',  1, GETDATE(), 0, 300,  NULL, NULL, NULL),
    (N'Hoàng Thị Lan',        'user4@gmail.com',   @PwdUser,  '0901000008', 'User',  1, GETDATE(), 1, 1500, NULL, NULL, NULL),
    (N'Bùi Văn Hậu',          'user5@gmail.com',   @PwdUser,  '0901000009', 'User',  1, GETDATE(), 1, 450,  NULL, NULL, NULL),
    (N'Dương Thu Trang',      'user6@gmail.com',   @PwdUser,  '0901000010', 'User',  1, GETDATE(), 1, 600,  NULL, NULL, NULL),
    (N'Đặng Quang Hải',       'user7@gmail.com',   @PwdUser,  '0901000011', 'User',  1, GETDATE(), 1, 950,  NULL, NULL, NULL),
    (N'Nguyễn Tiến Linh',     'user8@gmail.com',   @PwdUser,  '0901000012', 'User',  1, GETDATE(), 0, 100,  NULL, NULL, NULL);

-- Staff (3 records — phụ thuộc OwnerId nên insert sau)
DECLARE @IdOwner1 INT = (SELECT Id FROM Users WHERE Email = 'owner1@gmail.com');
DECLARE @IdOwner2 INT = (SELECT Id FROM Users WHERE Email = 'owner2@gmail.com');
DECLARE @IdOwner3 INT = (SELECT Id FROM Users WHERE Email = 'owner3@gmail.com');

INSERT INTO Users (HoTen, Email, MatKhau, SoDienThoai, VaiTro, IsActive, OwnerIdCuaStaff, NgayTao,
                   DaXacThucSdt, DiemHienTai, NganHang, SoTaiKhoan, TenTaiKhoan)
VALUES
    (N'Đào Việt Toàn',  'staff1@pitchhub.vn', @PwdStaff, '0901000013', 'Staff', 1, @IdOwner1, GETDATE(), 1, 0, NULL, NULL, NULL),
    (N'Lê Anh Khoa',    'staff2@pitchhub.vn', @PwdStaff, '0901000014', 'Staff', 1, @IdOwner1, GETDATE(), 1, 0, NULL, NULL, NULL),
    (N'Vũ Tuấn Minh',   'staff3@pitchhub.vn', @PwdStaff, '0901000015', 'Staff', 1, @IdOwner2, GETDATE(), 1, 0, NULL, NULL, NULL);
PRINT N'✔ Users: 15 (1 Admin + 3 Owner + 3 Staff + 8 User)';

-- Cache các Id Users để dùng cho các bảng phía sau
DECLARE @IdAdmin INT = (SELECT Id FROM Users WHERE Email = 'admin@pitchhub.vn');
DECLARE @IdStaff1 INT = (SELECT Id FROM Users WHERE Email = 'staff1@pitchhub.vn');
DECLARE @IdStaff2 INT = (SELECT Id FROM Users WHERE Email = 'staff2@pitchhub.vn');
DECLARE @IdStaff3 INT = (SELECT Id FROM Users WHERE Email = 'staff3@pitchhub.vn');
DECLARE @IdUser1 INT = (SELECT Id FROM Users WHERE Email = 'user1@gmail.com');
DECLARE @IdUser2 INT = (SELECT Id FROM Users WHERE Email = 'user2@gmail.com');
DECLARE @IdUser3 INT = (SELECT Id FROM Users WHERE Email = 'user3@gmail.com');
DECLARE @IdUser4 INT = (SELECT Id FROM Users WHERE Email = 'user4@gmail.com');
DECLARE @IdUser5 INT = (SELECT Id FROM Users WHERE Email = 'user5@gmail.com');
DECLARE @IdUser6 INT = (SELECT Id FROM Users WHERE Email = 'user6@gmail.com');
DECLARE @IdUser7 INT = (SELECT Id FROM Users WHERE Email = 'user7@gmail.com');
DECLARE @IdUser8 INT = (SELECT Id FROM Users WHERE Email = 'user8@gmail.com');

-- ============================================================
-- BƯỚC 7: SanBongs (12 sân)
-- ============================================================
INSERT INTO SanBongs (TenSan, DiaChi, Quan, ThanhPho, LoaiSan, LoaiCo,
                     HinhAnh, MoTa, DanhGiaTrungBinh, Latitude, Longitude,
                     TrangThaiDuyet, TyLeCoc, IsHidden, OwnerId,
                     DaKyHopDong, NgayKyHopDong, NoiDungHopDong,
                     ThoiGianGiuCho, ThoiGianHuyTruocGioDa,
                     PhanTramHoanCocDungHan, PhanTramHoanCocTreHan)
VALUES
    (N'Sân Cầu Giấy Sport',   N'12 Xuân Thủy',        N'Cầu Giấy',     N'Hà Nội', '5',  'Nhan tao',     '/img/san1.jpg',  N'Sân cỏ nhân tạo, đèn ban đêm',                4.5, 21.0362, 105.7826, 'DaDuyet',  0.30, 0, @IdOwner1, 1, DATEADD(MONTH, -6, GETDATE()), N'HĐ-2025-001', 15, 120, 1.00, 0.00),
    (N'Sân Đống Đa Arena',    N'45 Tây Sơn',          N'Đống Đa',      N'Hà Nội', '7',  'Nhan tao',     '/img/san2.jpg',  N'Sân 7 người rộng rãi, có mái che',            4.2, 21.0198, 105.8412, 'DaDuyet',  0.30, 0, @IdOwner1, 1, DATEADD(MONTH, -5, GETDATE()), N'HĐ-2025-002', 15, 120, 1.00, 0.00),
    (N'Sân Hoàng Mai FC',     N'78 Giải Phóng',       N'Hoàng Mai',    N'Hà Nội', '5',  'Tu nhien',     '/img/san3.jpg',  N'Sân cỏ tự nhiên, đá buổi sáng đẹp',           3.8, 20.9876, 105.8543, 'DaDuyet',  0.50, 0, @IdOwner1, 1, DATEADD(MONTH, -4, GETDATE()), N'HĐ-2025-003', 15, 180, 0.80, 0.00),
    (N'Sân Long Biên Star',   N'23 Nguyễn Văn Cừ',    N'Long Biên',    N'Hà Nội', '11', 'Nhan tao',     '/img/san4.jpg',  N'Sân 11 người tiêu chuẩn FIFA',                4.7, 21.0465, 105.8923, 'DaDuyet',  0.30, 0, @IdOwner2, 1, DATEADD(MONTH, -3, GETDATE()), N'HĐ-2025-004', 20, 240, 1.00, 0.50),
    (N'Sân Nam Từ Liêm Premier', N'88 Mỹ Đình',       N'Nam Từ Liêm',  N'Hà Nội', '7',  'Hybrid',       '/img/san5.jpg',  N'Sân hybrid cao cấp',                          4.6, 21.0157, 105.7647, 'DaDuyet',  0.30, 0, @IdOwner2, 1, DATEADD(MONTH, -3, GETDATE()), N'HĐ-2025-005', 15, 120, 1.00, 0.00),
    (N'Sân Tây Hồ Lake',      N'56 Lạc Long Quân',    N'Tây Hồ',       N'Hà Nội', '5',  'Nhan tao 5cm', '/img/san6.jpg',  N'Sân ven hồ thoáng mát',                       4.0, 21.0678, 105.8234, 'DaDuyet',  0.40, 0, @IdOwner2, 0, NULL,                          NULL,           15, 120, 1.00, 0.00),
    (N'Sân Ba Đình Center',   N'34 Hoàng Hoa Thám',   N'Ba Đình',      N'Hà Nội', '5',  'Nhan tao',     '/img/san7.jpg',  N'Sân trung tâm, gần Lăng Bác',                 4.3, 21.0379, 105.8333, 'DaDuyet',  0.30, 0, @IdOwner3, 1, DATEADD(MONTH, -2, GETDATE()), N'HĐ-2025-007', 15, 120, 1.00, 0.00),
    (N'Sân Thanh Xuân Hub',   N'99 Nguyễn Trãi',      N'Thanh Xuân',   N'Hà Nội', '7',  'Nhan tao 6cm', '/img/san8.jpg',  N'Sân 7 người tại Thanh Xuân, đỗ xe rộng',      4.1, 20.9956, 105.8033, 'DaDuyet',  0.30, 0, @IdOwner3, 1, DATEADD(MONTH, -2, GETDATE()), N'HĐ-2025-008', 15, 120, 1.00, 0.00),
    (N'Sân Hà Đông Futsal',   N'12 Quang Trung',      N'Hà Đông',      N'Hà Nội', 'F5', 'Trong nha',    '/img/san9.jpg',  N'Sân futsal trong nhà',                        4.4, 20.9711, 105.7793, 'DaDuyet',  0.30, 0, @IdOwner3, 1, DATEADD(MONTH, -1, GETDATE()), N'HĐ-2025-009', 10, 60,  1.00, 0.00),
    (N'Sân Hoàn Kiếm Mini',   N'8 Bà Triệu',          N'Hoàn Kiếm',    N'Hà Nội', '5',  'Nhan tao',     '/img/san10.jpg', N'Sân mini phố cổ, mở 24/7',                    3.9, 21.0234, 105.8521, 'DaDuyet',  0.40, 0, @IdOwner3, 0, NULL,                          NULL,           15, 120, 1.00, 0.00),
    (N'Sân Chờ Duyệt Test',   N'99 Test Street',      N'Nam Từ Liêm',  N'Hà Nội', '5',  'Nhan tao',     NULL,             N'Sân đang chờ Admin phê duyệt',                0.0, 21.0100, 105.7500, 'ChoDuyet', 0.30, 0, @IdOwner1, 0, NULL,                          NULL,           15, 120, 1.00, 0.00),
    (N'Sân Bị Từ Chối',       N'12 Test 2',           N'Bắc Từ Liêm',  N'Hà Nội', '7',  'Nhan tao',     NULL,             N'Sân không đạt yêu cầu',                       0.0, 21.0589, 105.7544, 'TuChoi',   0.30, 1, @IdOwner2, 0, NULL,                          NULL,           15, 120, 1.00, 0.00);
PRINT N'✔ SanBongs: 12 sân';

-- Cache Id Sân
DECLARE @San1 INT  = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Cầu Giấy Sport');
DECLARE @San2 INT  = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Đống Đa Arena');
DECLARE @San3 INT  = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Hoàng Mai FC');
DECLARE @San4 INT  = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Long Biên Star');
DECLARE @San5 INT  = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Nam Từ Liêm Premier');
DECLARE @San6 INT  = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Tây Hồ Lake');
DECLARE @San7 INT  = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Ba Đình Center');
DECLARE @San8 INT  = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Thanh Xuân Hub');
DECLARE @San9 INT  = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Hà Đông Futsal');
DECLARE @San10 INT = (SELECT Id FROM SanBongs WHERE TenSan = N'Sân Hoàn Kiếm Mini');

-- ============================================================
-- BƯỚC 8: AnhSanBongs (15 ảnh — mỗi sân có 1-2 ảnh)
-- ============================================================
INSERT INTO AnhSanBongs (SanBongId, DuongDan, LoaiAnh, ThuTu, MoTa, NgayThem, IsActive)
VALUES
    (@San1, '/img/san1_main.jpg',   'Upload', 0, N'Toàn cảnh sân Cầu Giấy',  GETDATE(), 1),
    (@San1, '/img/san1_phu.jpg',    'Upload', 1, N'Góc khán đài',             GETDATE(), 1),
    (@San2, '/img/san2_main.jpg',   'Upload', 0, N'Toàn cảnh sân Đống Đa',    GETDATE(), 1),
    (@San2, '/img/san2_mai.jpg',    'Upload', 1, N'Mái che',                   GETDATE(), 1),
    (@San3, '/img/san3_main.jpg',   'Upload', 0, N'Sân cỏ tự nhiên',           GETDATE(), 1),
    (@San4, '/img/san4_main.jpg',   'Upload', 0, N'Sân 11 người Long Biên',    GETDATE(), 1),
    (@San4, '/img/san4_locker.jpg', 'Upload', 1, N'Phòng thay đồ',             GETDATE(), 1),
    (@San5, '/img/san5_main.jpg',   'Upload', 0, N'Hybrid Premier',            GETDATE(), 1),
    (@San6, '/img/san6_main.jpg',   'Upload', 0, N'Sân ven Hồ Tây',            GETDATE(), 1),
    (@San7, '/img/san7_main.jpg',   'Upload', 0, N'Sân Ba Đình Center',        GETDATE(), 1),
    (@San8, '/img/san8_main.jpg',   'Upload', 0, N'Sân Thanh Xuân Hub',        GETDATE(), 1),
    (@San9, '/img/san9_main.jpg',   'Upload', 0, N'Futsal trong nhà',          GETDATE(), 1),
    (@San9, '/img/san9_den.jpg',    'Upload', 1, N'Hệ thống đèn',              GETDATE(), 1),
    (@San10,'/img/san10_main.jpg',  'Upload', 0, N'Sân Hoàn Kiếm Mini',        GETDATE(), 1),
    (@San10,'https://cdn.example/san10_extra.jpg', 'URL', 1, N'Khu lễ tân',    GETDATE(), 1);
PRINT N'✔ AnhSanBongs: 15 ảnh';

-- ============================================================
-- BƯỚC 9: StaffSanPhanCong (12 phân công — UNIQUE(StaffId,SanBongId))
-- ============================================================
INSERT INTO StaffSanPhanCong (StaffId, SanBongId, NgayGan)
VALUES
    (@IdStaff1, @San1, GETDATE()),
    (@IdStaff1, @San2, GETDATE()),
    (@IdStaff1, @San3, GETDATE()),
    (@IdStaff2, @San1, GETDATE()),
    (@IdStaff2, @San2, GETDATE()),
    (@IdStaff2, @San3, GETDATE()),
    (@IdStaff3, @San4, GETDATE()),
    (@IdStaff3, @San5, GETDATE()),
    (@IdStaff3, @San6, GETDATE()),
    (@IdStaff3, @San7, GETDATE()),
    (@IdStaff3, @San8, GETDATE()),
    (@IdStaff3, @San9, GETDATE());
PRINT N'✔ StaffSanPhanCong: 12 phân công';

-- ============================================================
-- BƯỚC 10: KhungGios (15+ khung — 3 sân x 5 khung)
-- ============================================================
INSERT INTO KhungGios (SanBongId, GioBatDau, GioKetThuc, Gia, GiaGioVang, GiaCuoiTuan, LoaiNgay, TrangThai)
VALUES
    -- Sân 1
    (@San1, '06:00', '07:30', 180000, 220000, 200000, 'TatCa', 'Trong'),
    (@San1, '07:30', '09:00', 180000, 220000, 200000, 'TatCa', 'Trong'),
    (@San1, '15:00', '16:30', 160000, 200000, 180000, 'TatCa', 'Trong'),
    (@San1, '16:30', '18:00', 200000, 260000, 240000, 'TatCa', 'Trong'),
    (@San1, '18:00', '19:30', 250000, 300000, 320000, 'TatCa', 'DaDat'),
    (@San1, '19:30', '21:00', 250000, 300000, 320000, 'TatCa', 'Trong'),
    -- Sân 2
    (@San2, '06:00', '07:30', 250000, 300000, 280000, 'TatCa', 'Trong'),
    (@San2, '17:00', '18:30', 320000, 380000, 400000, 'TatCa', 'DaDat'),
    (@San2, '18:30', '20:00', 350000, 420000, 440000, 'TatCa', 'Trong'),
    (@San2, '20:00', '21:30', 350000, 420000, 440000, 'TatCa', 'Trong'),
    -- Sân 4 (sân 11)
    (@San4, '06:00', '08:00', 500000, 600000, 580000, 'TatCa', 'Trong'),
    (@San4, '17:00', '19:00', 650000, 780000, 750000, 'TatCa', 'Trong'),
    (@San4, '19:00', '21:00', 650000, 780000, 750000, 'TatCa', 'Trong'),
    -- Sân 5
    (@San5, '17:00', '18:30', 320000, 380000, 400000, 'TatCa', 'Trong'),
    (@San5, '18:30', '20:00', 350000, 420000, 440000, 'TatCa', 'Trong');
PRINT N'✔ KhungGios: 15 khung giờ';

-- Cache 1 vài Id khung giờ để dùng cho DatSan
DECLARE @KG1_DaDat INT = (SELECT TOP 1 Id FROM KhungGios WHERE SanBongId = @San1 AND GioBatDau = '18:00' AND TrangThai = 'DaDat');
DECLARE @KG2_DaDat INT = (SELECT TOP 1 Id FROM KhungGios WHERE SanBongId = @San2 AND GioBatDau = '17:00' AND TrangThai = 'DaDat');
DECLARE @KG1_Trong INT = (SELECT TOP 1 Id FROM KhungGios WHERE SanBongId = @San1 AND GioBatDau = '19:30');
DECLARE @KG2_Trong INT = (SELECT TOP 1 Id FROM KhungGios WHERE SanBongId = @San2 AND GioBatDau = '18:30');
DECLARE @KG4_Trong INT = (SELECT TOP 1 Id FROM KhungGios WHERE SanBongId = @San4 AND GioBatDau = '17:00');
DECLARE @KG5_Trong INT = (SELECT TOP 1 Id FROM KhungGios WHERE SanBongId = @San5 AND GioBatDau = '17:00');

-- ============================================================
-- BƯỚC 11: DichVus (12 dịch vụ — gắn vào các sân)
-- ============================================================
DECLARE @DmNuoc INT = (SELECT Id FROM DanhMucDichVu WHERE TenDichVu = N'Nước uống');
DECLARE @DmBong INT = (SELECT Id FROM DanhMucDichVu WHERE TenDichVu = N'Thuê bóng');
DECLARE @DmTai  INT = (SELECT Id FROM DanhMucDichVu WHERE TenDichVu = N'Thuê trọng tài');
DECLARE @DmAo   INT = (SELECT Id FROM DanhMucDichVu WHERE TenDichVu = N'Thuê áo đấu');
DECLARE @DmQuay INT = (SELECT Id FROM DanhMucDichVu WHERE TenDichVu = N'Quay phim');
DECLARE @DmAn   INT = (SELECT Id FROM DanhMucDichVu WHERE TenDichVu = N'Đồ ăn nhẹ');

INSERT INTO DichVus (SanBongId, DanhMucDichVuId, TenDichVu, Gia, TonKho, IsActive, MoTa)
VALUES
    (@San1, @DmNuoc, N'Nước suối Lavie',     15000,  100, 1, N'Nước suối chai 500ml'),
    (@San1, @DmBong, N'Bóng Adidas Size 5',  30000,   10, 1, N'Bóng thi đấu tiêu chuẩn'),
    (@San1, @DmTai,  N'Trọng tài có cờ',     100000,   3, 1, N'Trọng tài kinh nghiệm'),
    (@San1, @DmAo,   N'Áo đấu có số',        20000,   22, 1, N'Áo có 2 màu, 11 số'),
    (@San2, @DmNuoc, N'Nước tăng lực Red Bull', 25000, 80,1, N'Nước tăng lực'),
    (@San2, @DmBong, N'Bóng Mikasa Size 4',  25000,   12, 1, N'Bóng futsal'),
    (@San2, @DmAo,   N'Áo đấu Premier',      30000,   20, 1, N'Áo cao cấp'),
    (@San4, @DmTai,  N'Trọng tài chính + biên', 200000, 2,1, N'Combo 3 trọng tài'),
    (@San4, @DmQuay, N'Quay full trận đấu',  500000,    2,1, N'Quay HD full trận'),
    (@San5, @DmAn,   N'Combo mì + nước',     35000,   50, 1, N'Mì gói + nước suối'),
    (@San7, @DmNuoc, N'Nước lọc',            10000,  200, 1, N'Nước lọc 500ml'),
    (@San9, @DmBong, N'Bóng Futsal Molten',  40000,    8, 1, N'Bóng futsal chuyên dụng');
PRINT N'✔ DichVus: 12 dịch vụ';

-- Cache Id Dịch vụ
DECLARE @Dv_S1_Nuoc INT = (SELECT TOP 1 Id FROM DichVus WHERE SanBongId = @San1 AND DanhMucDichVuId = @DmNuoc);
DECLARE @Dv_S1_Bong INT = (SELECT TOP 1 Id FROM DichVus WHERE SanBongId = @San1 AND DanhMucDichVuId = @DmBong);
DECLARE @Dv_S1_Tai  INT = (SELECT TOP 1 Id FROM DichVus WHERE SanBongId = @San1 AND DanhMucDichVuId = @DmTai);
DECLARE @Dv_S2_Bong INT = (SELECT TOP 1 Id FROM DichVus WHERE SanBongId = @San2 AND DanhMucDichVuId = @DmBong);
DECLARE @Dv_S2_Nuoc INT = (SELECT TOP 1 Id FROM DichVus WHERE SanBongId = @San2 AND DanhMucDichVuId = @DmNuoc);
DECLARE @Dv_S4_Tai  INT = (SELECT TOP 1 Id FROM DichVus WHERE SanBongId = @San4 AND DanhMucDichVuId = @DmTai);

-- ============================================================
-- BƯỚC 12: DatSans (15 đơn — bao gồm nhiều trạng thái)
-- ============================================================
INSERT INTO DatSans (UserId, KhungGioId, NgayThiDau, TienCoc, TongTien, MaXacNhan,
                    TrangThai, StaffCheckInId, StaffCheckOutId, LoaiSuCo, GhiChuSuCo, GhiChuStaff,
                    ThoiGianTao, NguonHuy, LoaiHoanCoc, PhanTramHoan, SoTienDaHoan,
                    GiaiDauId, LaDummyBooking)
VALUES
    (@IdUser1, @KG1_DaDat,  DATEADD(DAY,  2, GETDATE()),  75000, 250000, 'BK00000001', 'DaXacNhan', NULL,      NULL,      NULL,       NULL, NULL, GETDATE(), NULL, NULL, NULL, NULL, NULL, 0),
    (@IdUser2, @KG2_DaDat,  DATEADD(DAY,  3, GETDATE()), 105000, 350000, 'BK00000002', 'DaXacNhan', NULL,      NULL,      NULL,       NULL, NULL, GETDATE(), NULL, NULL, NULL, NULL, NULL, 0),
    (@IdUser3, @KG1_Trong,  DATEADD(DAY,  4, GETDATE()),  75000,      0, 'BK00000003', 'ChoDuyet',  NULL,      NULL,      NULL,       NULL, NULL, GETDATE(), NULL, NULL, NULL, NULL, NULL, 0),
    (@IdUser4, @KG2_Trong,  DATEADD(DAY,  5, GETDATE()), 105000, 350000, 'BK00000004', 'DaXacNhan', NULL,      NULL,      NULL,       NULL, NULL, GETDATE(), NULL, NULL, NULL, NULL, NULL, 0),
    (@IdUser5, @KG4_Trong,  DATEADD(DAY,  6, GETDATE()), 195000, 650000, 'BK00000005', 'DaXacNhan', NULL,      NULL,      NULL,       NULL, NULL, GETDATE(), NULL, NULL, NULL, NULL, NULL, 0),
    -- Đang sử dụng (Staff đã check-in)
    (@IdUser6, @KG5_Trong,  DATEADD(HOUR, 1, GETDATE()),  96000, 320000, 'BK00000006', 'DangSuDung',@IdStaff3, NULL,      NULL,       NULL, N'Khách đã đến đúng giờ', GETDATE(), NULL, NULL, NULL, NULL, NULL, 0),
    -- Đã hoàn thành
    (@IdUser1, @KG1_Trong,  DATEADD(DAY, -3, GETDATE()),  75000, 250000, 'BK00000007', 'HoanThanh', @IdStaff1, @IdStaff1, NULL,       NULL, N'Trận đấu tốt, khách hài lòng', DATEADD(DAY,-10,GETDATE()), NULL, NULL, NULL, NULL, NULL, 0),
    (@IdUser2, @KG2_Trong,  DATEADD(DAY, -5, GETDATE()), 105000, 350000, 'BK00000008', 'HoanThanh', @IdStaff1, @IdStaff2, NULL,       NULL, NULL, DATEADD(DAY,-12,GETDATE()), NULL, NULL, NULL, NULL, NULL, 0),
    (@IdUser4, @KG4_Trong,  DATEADD(DAY,-10, GETDATE()), 195000, 650000, 'BK00000009', 'HoanThanh', @IdStaff3, @IdStaff3, NULL,       NULL, NULL, DATEADD(DAY,-15,GETDATE()), NULL, NULL, NULL, NULL, NULL, 0),
    -- Đã hủy (User hủy đúng hạn)
    (@IdUser7, @KG1_Trong,  DATEADD(DAY, -1, GETDATE()),  75000, 250000, 'BK00000010', 'DaHuy',     NULL,      NULL,      NULL,       NULL, NULL, DATEADD(DAY,-7,GETDATE()), 'User',  'DungHan', 1.00,  75000, NULL, 0),
    -- Đã hủy (User hủy trễ hạn — phạt)
    (@IdUser8, @KG2_Trong,  DATEADD(DAY,  1, GETDATE()), 105000, 350000, 'BK00000011', 'DaHuy',     NULL,      NULL,      NULL,       NULL, NULL, DATEADD(DAY,-1,GETDATE()), 'User',  'TreHan',  0.00,      0, NULL, 0),
    -- Sự cố No-show
    (@IdUser5, @KG5_Trong,  DATEADD(DAY, -2, GETDATE()),  96000, 320000, 'BK00000012', 'HoanThanh', @IdStaff3, @IdStaff3, 'NoShow',   N'Khách không tới', NULL, DATEADD(DAY,-8,GETDATE()), NULL, NULL, NULL, NULL, NULL, 0),
    -- Sự cố Hỏng hóc
    (@IdUser3, @KG1_Trong,  DATEADD(DAY, -7, GETDATE()),  75000, 250000, 'BK00000013', 'HoanThanh', @IdStaff1, @IdStaff1, 'HongHoc',  N'Hỏng lưới khung thành',    N'Đã sửa sau trận', DATEADD(DAY,-14,GETDATE()), NULL, NULL, NULL, NULL, NULL, 0),
    -- Hủy do Owner
    (@IdUser6, @KG1_Trong,  DATEADD(DAY,  7, GETDATE()),  75000, 250000, 'BK00000014', 'DaHuy',     NULL,      NULL,      NULL,       NULL, NULL, DATEADD(DAY,-1,GETDATE()), 'Owner', 'DungHan', 1.00,  75000, NULL, 0),
    -- Đơn ChoDuyet mới nhất
    (@IdUser7, @KG2_Trong,  DATEADD(DAY,  8, GETDATE()), 105000,      0, 'BK00000015', 'ChoDuyet',  NULL,      NULL,      NULL,       NULL, NULL, GETDATE(), NULL, NULL, NULL, NULL, NULL, 0);
PRINT N'✔ DatSans: 15 đơn';

-- Cache vài Id DatSan
DECLARE @Ds1 INT  = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000001');
DECLARE @Ds2 INT  = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000002');
DECLARE @Ds3 INT  = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000003');
DECLARE @Ds4 INT  = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000004');
DECLARE @Ds5 INT  = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000005');
DECLARE @Ds6 INT  = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000006');
DECLARE @Ds7 INT  = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000007');
DECLARE @Ds8 INT  = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000008');
DECLARE @Ds9 INT  = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000009');
DECLARE @Ds10 INT = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000010');
DECLARE @Ds11 INT = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000011');
DECLARE @Ds12 INT = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000012');
DECLARE @Ds13 INT = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000013');
DECLARE @Ds14 INT = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000014');
DECLARE @Ds15 INT = (SELECT Id FROM DatSans WHERE MaXacNhan = 'BK00000015');

-- ============================================================
-- BƯỚC 13: DatSan_DichVus (12 dịch vụ kèm đơn)
-- ============================================================
INSERT INTO DatSan_DichVus (DatSanId, DichVuId, SoLuong)
VALUES
    (@Ds1, @Dv_S1_Nuoc, 10),
    (@Ds1, @Dv_S1_Bong, 1),
    (@Ds2, @Dv_S2_Bong, 1),
    (@Ds2, @Dv_S2_Nuoc, 8),
    (@Ds4, @Dv_S2_Bong, 1),
    (@Ds4, @Dv_S2_Nuoc, 12),
    (@Ds5, @Dv_S4_Tai, 1),
    (@Ds6, @Dv_S4_Tai, 1),
    (@Ds7, @Dv_S1_Nuoc, 6),
    (@Ds7, @Dv_S1_Tai, 1),
    (@Ds8, @Dv_S2_Bong, 1),
    (@Ds9, @Dv_S4_Tai, 1);
PRINT N'✔ DatSan_DichVus: 12 dòng dịch vụ';

-- ============================================================
-- BƯỚC 14: DanhGias (12 đánh giá — UNIQUE(UserId, DatSanId))
-- ============================================================
INSERT INTO DanhGias (SanBongId, UserId, DatSanId, SoSao, NhanXet, NgayDanhGia, SoSaoCoSoVatChat, SoSaoNhanVien, PhanHoiOwner, NgayPhanHoi)
VALUES
    (@San1, @IdUser1, @Ds7,  5, N'Sân đẹp, phục vụ tốt!',                       DATEADD(DAY,-2,GETDATE()), 5, 5, N'Cảm ơn quý khách!', DATEADD(DAY,-1,GETDATE())),
    (@San2, @IdUser2, @Ds8,  4, N'Sân ổn, hơi đông xe ngoài giờ cao điểm.',      DATEADD(DAY,-4,GETDATE()), 4, 4, NULL, NULL),
    (@San4, @IdUser4, @Ds9,  5, N'Sân 11 chất lượng FIFA, đáng tiền.',           DATEADD(DAY,-8,GETDATE()), 5, 5, N'Hẹn gặp lại!',      DATEADD(DAY,-7,GETDATE())),
    (@San5, @IdUser5, @Ds12, 3, N'Khách đoàn đến trễ, mình không kịp đến nữa.',  DATEADD(DAY,-1,GETDATE()), 4, 2, NULL, NULL),
    (@San1, @IdUser3, @Ds13, 2, N'Lưới khung thành rách lúc đang đá, khá phiền.',DATEADD(DAY,-5,GETDATE()), 2, 3, N'Đã khắc phục, xin lỗi quý khách!', DATEADD(DAY,-4,GETDATE())),
    -- Thêm 7 đánh giá ảo (mượn các Ds trong quá khứ)
    (@San1, @IdUser5, @Ds1,  4, N'Lần đầu đá ở đây, ổn.',                        GETDATE(), 4, 4, NULL, NULL),
    (@San2, @IdUser6, @Ds2,  5, N'Tuyệt vời!',                                   GETDATE(), 5, 5, NULL, NULL),
    (@San1, @IdUser7, @Ds3,  3, N'Bình thường.',                                 GETDATE(), 3, 3, NULL, NULL),
    (@San2, @IdUser8, @Ds4,  4, N'Sân chuẩn, sẽ quay lại.',                      GETDATE(), 4, 4, NULL, NULL),
    (@San4, @IdUser1, @Ds5,  5, N'Phòng thay đồ sạch sẽ.',                       GETDATE(), 5, 5, NULL, NULL),
    (@San5, @IdUser2, @Ds6,  4, N'Hybrid pitch đá êm chân.',                     GETDATE(), 4, 4, NULL, NULL),
    (@San2, @IdUser4, @Ds11, 1, N'Sân không cho hủy gần giờ, mất tiền cọc.',     GETDATE(), 3, 1, N'Quy định đã thông báo trước.', GETDATE());
PRINT N'✔ DanhGias: 12 đánh giá';

-- ============================================================
-- BƯỚC 15: Matchmakings (10 — DatSanId UNIQUE)
-- ============================================================
INSERT INTO Matchmakings (DatSanId, UserId, TieuDe, MoTa, SoNguoiCanThem, TrangThai, NgayDang, LyDoHuy)
VALUES
    (@Ds1,  @IdUser1, N'Tuyển 3 người đá sân 5',     N'Cần 3 người trình độ trung bình', 3, 'DangTim', GETDATE(), NULL),
    (@Ds2,  @IdUser2, N'Cần 2 cầu thủ sân 7',         N'Đá vui là chính',                 2, 'DangTim', GETDATE(), NULL),
    (@Ds3,  @IdUser3, N'Cần đối thủ giao hữu',        N'Giao hữu nhẹ nhàng',              5, 'DangTim', GETDATE(), NULL),
    (@Ds4,  @IdUser4, N'Tuyển 4 người sân 7',         N'Trình độ khá',                    4, 'DaDu',    GETDATE(), NULL),
    (@Ds5,  @IdUser5, N'Cần 8 người sân 11',          N'Đá đủ đội hình',                  8, 'DangTim', GETDATE(), NULL),
    (@Ds7,  @IdUser1, N'Đã tìm xong',                 N'Hủy do đã đủ người',              0, 'DaDong',  DATEADD(DAY,-3,GETDATE()), N'Đã đủ người trước trận'),
    (@Ds8,  @IdUser2, N'Cần 1 thủ môn',               N'Trận đã kết thúc',                1, 'DaDong',  DATEADD(DAY,-5,GETDATE()), N'Đã đá xong'),
    (@Ds9,  @IdUser4, N'Sân 11 — cần đối thủ',        N'Đã tìm thấy đối thủ',             0, 'DaDu',    DATEADD(DAY,-10,GETDATE()), NULL),
    (@Ds12, @IdUser5, N'Cần 4 người sân Futsal',      N'Thử trình độ trung bình',         4, 'DaDong',  DATEADD(DAY,-2,GETDATE()), N'Không đủ người'),
    (@Ds15, @IdUser7, N'Tìm đối sân 7 cuối tuần',     N'Đá giao hữu cuối tuần',           7, 'DangTim', GETDATE(), NULL);
PRINT N'✔ Matchmakings: 10';

-- ============================================================
-- BƯỚC 16: KhieuNais (12)
-- ============================================================
INSERT INTO KhieuNais (DatSanId, UserId, LyDo, TrangThai, GhiChuAdmin, SoTienHoan, NgayGui, NgayXuLy, AdminXuLyId)
VALUES
    (@Ds13, @IdUser3, N'Lưới khung thành rách giữa trận, không thể đá tiếp.',      'DaHoanCoc', N'Hoàn 50% cọc do sự cố từ phía sân.', 37500, DATEADD(DAY,-5,GETDATE()), DATEADD(DAY,-4,GETDATE()), @IdAdmin),
    (@Ds12, @IdUser5, N'Bị trừ no-show không hợp lý, có bằng chứng đã tới.',        'TuChoi',    N'Đã kiểm tra camera, khách thực sự không tới.', NULL, DATEADD(DAY,-1,GETDATE()), DATEADD(HOUR,-12,GETDATE()), @IdAdmin),
    (@Ds11, @IdUser8, N'Bị hủy trễ hạn không nhận được tiền hoàn cọc.',             'TuChoi',    N'Quy định trễ hạn không hoàn cọc.', NULL, DATEADD(HOUR,-2,GETDATE()), DATEADD(HOUR,-1,GETDATE()), @IdAdmin),
    (@Ds7,  @IdUser1, N'Sân bẩn, đèn không đủ sáng.',                                'DaHoanCoc', N'Owner đã thừa nhận lỗi.', 50000, DATEADD(DAY,-2,GETDATE()), DATEADD(DAY,-1,GETDATE()), @IdAdmin),
    (@Ds8,  @IdUser2, N'Staff thiếu chuyên nghiệp.',                                 'ChoXuLy',   NULL, NULL, DATEADD(HOUR,-6,GETDATE()), NULL, NULL),
    (@Ds9,  @IdUser4, N'Trận đấu bị ảnh hưởng do mất điện.',                          'ChoXuLy',   NULL, NULL, DATEADD(HOUR,-3,GETDATE()), NULL, NULL),
    (@Ds6,  @IdUser6, N'Yêu cầu đổi giờ nhưng không được hỗ trợ.',                    'ChoXuLy',   NULL, NULL, DATEADD(HOUR,-1,GETDATE()), NULL, NULL),
    (@Ds5,  @IdUser5, N'Tính tiền dịch vụ thừa.',                                     'ChoXuLy',   NULL, NULL, DATEADD(MINUTE,-30,GETDATE()), NULL, NULL),
    (@Ds4,  @IdUser4, N'Sân không như mô tả trên web.',                               'TuChoi',    N'Đã đối chiếu, sân đúng mô tả.', NULL, DATEADD(DAY,-1,GETDATE()), DATEADD(HOUR,-12,GETDATE()), @IdAdmin),
    (@Ds3,  @IdUser3, N'Bị giữ chỗ quá lâu chưa được xác nhận.',                      'DaHoanCoc', N'Hoàn cọc đầy đủ.', 75000, DATEADD(DAY,-1,GETDATE()), DATEADD(HOUR,-6,GETDATE()), @IdAdmin),
    (@Ds2,  @IdUser2, N'Phòng thay đồ thiếu nước nóng.',                              'ChoXuLy',   NULL, NULL, DATEADD(MINUTE,-15,GETDATE()), NULL, NULL),
    (@Ds1,  @IdUser1, N'Bóng được cấp bị xì hơi giữa trận.',                          'DaHoanCoc', N'Bồi thường tiền thuê bóng.', 30000, DATEADD(HOUR,-4,GETDATE()), DATEADD(HOUR,-2,GETDATE()), @IdAdmin);
PRINT N'✔ KhieuNais: 12';

-- ============================================================
-- BƯỚC 17: AuditLogs (15)
-- ============================================================
INSERT INTO AuditLogs (UserId, VaiTro, HanhDong, DoiTuong, DoiTuongId, MoTa, IpAddress, ThoiGian)
VALUES
    (@IdAdmin,  'Admin', N'PHE_DUYET_SAN',   'SanBong', @San1,  N'Phê duyệt sân Cầu Giấy Sport',   '127.0.0.1',  DATEADD(DAY,-30,GETDATE())),
    (@IdAdmin,  'Admin', N'PHE_DUYET_SAN',   'SanBong', @San2,  N'Phê duyệt sân Đống Đa Arena',     '127.0.0.1',  DATEADD(DAY,-28,GETDATE())),
    (@IdAdmin,  'Admin', N'TU_CHOI_SAN',     'SanBong', 12,     N'Từ chối sân không đạt yêu cầu',   '127.0.0.1',  DATEADD(DAY,-20,GETDATE())),
    (@IdOwner1, 'Owner', N'TAO_SAN',         'SanBong', @San3,  N'Tạo sân mới',                     '192.168.1.10',DATEADD(DAY,-25,GETDATE())),
    (@IdOwner2, 'Owner', N'TAO_SAN',         'SanBong', @San4,  N'Tạo sân Long Biên Star',          '192.168.1.20',DATEADD(DAY,-20,GETDATE())),
    (@IdOwner1, 'Owner', N'GAN_STAFF',       'Staff',   @IdStaff1, N'Gán Staff1 vào sân 1, 2, 3',  '192.168.1.10',DATEADD(DAY,-15,GETDATE())),
    (@IdUser1,  'User',  N'DAT_SAN',         'DatSan',  @Ds1,   N'Đặt sân thành công',              '113.22.10.5', DATEADD(DAY,-1,GETDATE())),
    (@IdUser2,  'User',  N'DAT_SAN',         'DatSan',  @Ds2,   N'Đặt sân thành công',              '113.22.10.7', DATEADD(DAY,-1,GETDATE())),
    (@IdStaff1, 'Staff', N'CHECK_IN',        'DatSan',  @Ds7,   N'Check-in khách đến đúng giờ',     '192.168.1.50',DATEADD(DAY,-3,GETDATE())),
    (@IdStaff1, 'Staff', N'CHECK_OUT',       'DatSan',  @Ds7,   N'Hoàn tất check-out',              '192.168.1.50',DATEADD(DAY,-3,GETDATE())),
    (@IdStaff3, 'Staff', N'GHI_SU_CO',       'DatSan',  @Ds12,  N'No-show — khách không tới',       '192.168.1.51',DATEADD(DAY,-2,GETDATE())),
    (@IdAdmin,  'Admin', N'XU_LY_KHIEU_NAI', 'KhieuNai', 1,     N'Hoàn cọc khiếu nại lưới khung thành', '127.0.0.1', DATEADD(DAY,-4,GETDATE())),
    (@IdUser5,  'User',  N'HUY_DON',         'DatSan',  @Ds10,  N'Hủy đơn đúng hạn',                '113.22.11.3', DATEADD(DAY,-7,GETDATE())),
    (@IdOwner2, 'Owner', N'CAP_NHAT_KHUNG_GIO','KhungGio', @KG4_Trong, N'Tăng giá khung giờ vàng', '192.168.1.20', DATEADD(DAY,-2,GETDATE())),
    (@IdUser1,  'User',  N'DOI_MAT_KHAU',    'User',    @IdUser1, N'Đổi mật khẩu thành công',       '113.22.10.5', DATEADD(HOUR,-5,GETDATE()));
PRINT N'✔ AuditLogs: 15';

-- ============================================================
-- BƯỚC 18: Vouchers (12)
-- ============================================================
INSERT INTO Vouchers (MaVoucher, TenVoucher, MoTa, LoaiGiam, GiaTriGiam, GiamToiDa, DiemCanDoi, SoNgayHieuLuc, IsActive, NgayTao, OwnerId, SanBongId, LoaiPhatHanh, SoLuotConLai)
VALUES
    ('NEWUSER10',     N'Giảm 10% đơn đầu tiên',          N'Áp dụng cho người dùng mới',                  'PhanTram', 10,    50000,   500, 30,  1, GETDATE(), NULL,      NULL,  'HeThong',        500),
    ('SALE20K',       N'Giảm 20.000đ cho đơn từ 200K',   N'Voucher hệ thống',                            'TienMat',  20000, 20000,  1000, 30,  1, GETDATE(), NULL,      NULL,  'HeThong',        300),
    ('VIP15',         N'Giảm 15% cho VIP',               N'Voucher hệ thống giảm 15%',                   'PhanTram', 15,    100000, 1500, 60,  1, GETDATE(), NULL,      NULL,  'HeThong',        200),
    ('FREESHIP',      N'Miễn phí dịch vụ trọng tài',     N'Voucher hệ thống — giảm 100K',                'TienMat',  100000,100000, 3000, 30,  1, GETDATE(), NULL,      NULL,  'HeThong',        100),
    ('XMAS50',        N'Giảm 50K dịp Giáng sinh',        N'Voucher hệ thống lễ hội',                     'TienMat',  50000, 50000,  1500, 15,  1, GETDATE(), NULL,      NULL,  'HeThong',        500),
    ('OWNER1_10',     N'Sân Cầu Giấy giảm 10%',          N'Khuyến mãi riêng sân Cầu Giấy Sport',         'PhanTram', 10,    30000,   200, 30,  1, GETDATE(), @IdOwner1, @San1, 'OwnerKhuyenMai', 200),
    ('OWNER1_20K',    N'Sân Đống Đa giảm 20K',           N'Khuyến mãi riêng sân Đống Đa Arena',           'TienMat',  20000, 20000,   300, 14,  1, GETDATE(), @IdOwner1, @San2, 'OwnerKhuyenMai', 150),
    ('OWNER2_15',     N'Sân Long Biên giảm 15%',         N'Khuyến mãi đặc biệt sân 11',                   'PhanTram', 15,    100000,  500, 30,  1, GETDATE(), @IdOwner2, @San4, 'OwnerKhuyenMai', 100),
    ('OWNER2_FUTSAL', N'Hà Đông Futsal -30K',            N'Khuyến mãi sân futsal',                        'TienMat',  30000, 30000,   400, 21,  1, GETDATE(), @IdOwner3, @San9, 'OwnerKhuyenMai', 80),
    ('SUMMER25',      N'Hè rực rỡ giảm 25%',             N'Voucher hệ thống mùa hè',                      'PhanTram', 25,    80000,  2500, 45,  1, GETDATE(), NULL,      NULL,  'HeThong',        300),
    ('EXPIRED',       N'Giảm 5K (đã hết hạn)',           N'Voucher đã hết hạn để test',                   'TienMat',  5000,  5000,    100, 1,   0, DATEADD(DAY,-60,GETDATE()), NULL, NULL, 'HeThong',         0),
    ('FLASH100',      N'Flash sale -100K',               N'Voucher flash chỉ dùng được 50 lượt',          'TienMat',  100000,100000, 5000, 7,   1, GETDATE(), NULL,      NULL,  'HeThong',         50);
PRINT N'✔ Vouchers: 12';

DECLARE @Vc1 INT = (SELECT Id FROM Vouchers WHERE MaVoucher = 'NEWUSER10');
DECLARE @Vc2 INT = (SELECT Id FROM Vouchers WHERE MaVoucher = 'SALE20K');
DECLARE @Vc3 INT = (SELECT Id FROM Vouchers WHERE MaVoucher = 'VIP15');
DECLARE @Vc4 INT = (SELECT Id FROM Vouchers WHERE MaVoucher = 'FREESHIP');
DECLARE @Vc5 INT = (SELECT Id FROM Vouchers WHERE MaVoucher = 'XMAS50');
DECLARE @Vc6 INT = (SELECT Id FROM Vouchers WHERE MaVoucher = 'OWNER1_10');
DECLARE @Vc7 INT = (SELECT Id FROM Vouchers WHERE MaVoucher = 'OWNER1_20K');
DECLARE @Vc10 INT = (SELECT Id FROM Vouchers WHERE MaVoucher = 'SUMMER25');

-- ============================================================
-- BƯỚC 19: UserVouchers (12 — MaSuDung UNIQUE)
-- ============================================================
INSERT INTO UserVouchers (UserId, VoucherId, MaSuDung, NgayDoi, NgayHetHan, IsUsed, NgaySuDung, DatSanId)
VALUES
    (@IdUser1, @Vc1,  'USE-U1-V1-0001', GETDATE(),                DATEADD(DAY,30,GETDATE()), 0, NULL,                            NULL),
    (@IdUser1, @Vc3,  'USE-U1-V3-0002', DATEADD(DAY,-5,GETDATE()),DATEADD(DAY,55,GETDATE()), 1, DATEADD(DAY,-3,GETDATE()),       @Ds7),
    (@IdUser2, @Vc2,  'USE-U2-V2-0003', GETDATE(),                DATEADD(DAY,30,GETDATE()), 0, NULL,                            NULL),
    (@IdUser2, @Vc4,  'USE-U2-V4-0004', DATEADD(DAY,-10,GETDATE()),DATEADD(DAY,20,GETDATE()),1, DATEADD(DAY,-5,GETDATE()),       @Ds8),
    (@IdUser3, @Vc1,  'USE-U3-V1-0005', GETDATE(),                DATEADD(DAY,30,GETDATE()), 0, NULL,                            NULL),
    (@IdUser4, @Vc5,  'USE-U4-V5-0006', GETDATE(),                DATEADD(DAY,15,GETDATE()), 0, NULL,                            NULL),
    (@IdUser4, @Vc6,  'USE-U4-V6-0007', GETDATE(),                DATEADD(DAY,30,GETDATE()), 0, NULL,                            NULL),
    (@IdUser5, @Vc7,  'USE-U5-V7-0008', GETDATE(),                DATEADD(DAY,14,GETDATE()), 0, NULL,                            NULL),
    (@IdUser5, @Vc10, 'USE-U5-VA-0009', DATEADD(DAY,-2,GETDATE()),DATEADD(DAY,43,GETDATE()), 0, NULL,                            NULL),
    (@IdUser6, @Vc2,  'USE-U6-V2-0010', GETDATE(),                DATEADD(DAY,30,GETDATE()), 1, GETDATE(),                       @Ds6),
    (@IdUser7, @Vc3,  'USE-U7-V3-0011', GETDATE(),                DATEADD(DAY,60,GETDATE()), 0, NULL,                            NULL),
    (@IdUser8, @Vc5,  'USE-U8-V5-0012', GETDATE(),                DATEADD(DAY,15,GETDATE()), 0, NULL,                            NULL);
PRINT N'✔ UserVouchers: 12';

-- ============================================================
-- BƯỚC 20: SanYeuThiches (12 — UNIQUE(UserId, SanBongId))
-- ============================================================
INSERT INTO SanYeuThichs (UserId, SanBongId, NgayThem)
VALUES
    (@IdUser1, @San1, GETDATE()),
    (@IdUser1, @San2, GETDATE()),
    (@IdUser1, @San4, GETDATE()),
    (@IdUser2, @San1, GETDATE()),
    (@IdUser2, @San5, GETDATE()),
    (@IdUser3, @San3, GETDATE()),
    (@IdUser4, @San4, GETDATE()),
    (@IdUser4, @San7, GETDATE()),
    (@IdUser5, @San5, GETDATE()),
    (@IdUser6, @San9, GETDATE()),
    (@IdUser7, @San2, GETDATE()),
    (@IdUser8, @San10, GETDATE());
PRINT N'✔ SanYeuThiches: 12';

-- ============================================================
-- BƯỚC 21: DiemThuongLogs (15)
-- ============================================================
INSERT INTO DiemThuongLogs (UserId, SoDiem, SoDuSauGd, LoaiSuKien, GhiChu, DatSanId, ThoiGian)
VALUES
    (@IdUser1,  500, 500,  N'DangKy',           N'Tặng 500 điểm chào mừng',     NULL,  DATEADD(DAY,-60,GETDATE())),
    (@IdUser1,  200, 700,  N'DatSan',           N'Đặt sân Cầu Giấy',             @Ds7,  DATEADD(DAY,-30,GETDATE())),
    (@IdUser1,  500, 1200, N'DatSan',           N'Đặt sân Đống Đa',              @Ds1,  DATEADD(DAY,-1,GETDATE())),
    (@IdUser2,  500, 500,  N'DangKy',           N'Tặng 500 điểm chào mừng',     NULL,  DATEADD(DAY,-50,GETDATE())),
    (@IdUser2,  300, 800,  N'DatSan',           N'Đặt sân Đống Đa',              @Ds8,  DATEADD(DAY,-12,GETDATE())),
    (@IdUser3,  300, 300,  N'DangKy',           N'Tặng 300 điểm chào mừng',     NULL,  DATEADD(DAY,-15,GETDATE())),
    (@IdUser4,  500, 500,  N'DangKy',           N'Tặng 500 điểm chào mừng',     NULL,  DATEADD(DAY,-90,GETDATE())),
    (@IdUser4,  1000,1500, N'DatSan',           N'Đặt sân Long Biên Star',       @Ds9,  DATEADD(DAY,-15,GETDATE())),
    (@IdUser5,  500, 500,  N'DangKy',           N'Tặng 500 điểm chào mừng',     NULL,  DATEADD(DAY,-45,GETDATE())),
    (@IdUser5, -300, 200,  N'PhatNoShow',       N'Trừ 300 điểm do no-show',      @Ds12, DATEADD(DAY,-2,GETDATE())),
    (@IdUser6,  500, 500,  N'DangKy',           N'Tặng 500 điểm chào mừng',     NULL,  DATEADD(DAY,-20,GETDATE())),
    (@IdUser6,  100, 600,  N'DatSan',           N'Đặt sân Futsal',               @Ds6,  GETDATE()),
    (@IdUser7,  500, 500,  N'DangKy',           N'Tặng 500 điểm chào mừng',     NULL,  DATEADD(DAY,-10,GETDATE())),
    (@IdUser7,  500, 1000, N'DanhGia5Sao',      N'Thưởng đánh giá 5 sao',        @Ds7,  DATEADD(DAY,-1,GETDATE())),
    (@IdUser8,  100, 100,  N'DangKy',           N'Tặng 100 điểm (test)',        NULL,  DATEADD(DAY,-5,GETDATE()));
PRINT N'✔ DiemThuongLogs: 15';

-- ============================================================
-- BƯỚC 22: OtpCodes (12)
-- ============================================================
INSERT INTO OtpCodes (UserId, SoDienThoai, MaOtp, NgayTao, NgayHetHan, IsUsed)
VALUES
    (@IdUser1, '0901000005', '123456', DATEADD(MINUTE,-3,GETDATE()),  DATEADD(MINUTE,2,GETDATE()),  0),
    (@IdUser2, '0901000006', '234567', DATEADD(MINUTE,-10,GETDATE()), DATEADD(MINUTE,-5,GETDATE()), 1),
    (@IdUser3, '0901000007', '345678', DATEADD(MINUTE,-30,GETDATE()), DATEADD(MINUTE,-25,GETDATE()),0),
    (@IdUser3, '0901000007', '987654', DATEADD(MINUTE,-1,GETDATE()),  DATEADD(MINUTE,4,GETDATE()),  0),
    (@IdUser4, '0901000008', '456789', DATEADD(DAY,-1,GETDATE()),     DATEADD(DAY,-1,GETDATE()),    1),
    (@IdUser5, '0901000009', '567890', DATEADD(MINUTE,-2,GETDATE()),  DATEADD(MINUTE,3,GETDATE()),  0),
    (@IdUser6, '0901000010', '678901', DATEADD(MINUTE,-15,GETDATE()), DATEADD(MINUTE,-10,GETDATE()),0),
    (@IdUser7, '0901000011', '789012', DATEADD(MINUTE,-4,GETDATE()),  DATEADD(MINUTE,1,GETDATE()),  1),
    (@IdUser8, '0901000012', '890123', DATEADD(MINUTE,-20,GETDATE()), DATEADD(MINUTE,-15,GETDATE()),0),
    (@IdUser8, '0901000012', '098765', GETDATE(),                     DATEADD(MINUTE,5,GETDATE()),  0),
    (@IdOwner1,'0901000002', '111222', DATEADD(MINUTE,-5,GETDATE()),  GETDATE(),                     0),
    (@IdOwner2,'0901000003', '333444', GETDATE(),                     DATEADD(MINUTE,5,GETDATE()),  0);
PRINT N'✔ OtpCodes: 12';

-- ============================================================
-- BƯỚC 23: GiaoDichHoanCocs (12)
-- ============================================================
INSERT INTO GiaoDichHoanCocs (DatSanId, ThoiGianGiaoDich, SoTien, VaiTroNguoiKhoiTao, NguoiKhoiTaoId, TrangThaiHoan, GhiChu)
VALUES
    (@Ds10, DATEADD(DAY,-7,GETDATE()), 75000,  'User',  @IdUser7,  'DaHoan',  N'Hoàn cọc đầy đủ do hủy đúng hạn'),
    (@Ds11, DATEADD(DAY,-1,GETDATE()), 0,      'User',  @IdUser8,  'TuChoi',  N'Hủy trễ hạn — không hoàn cọc'),
    (@Ds13, DATEADD(DAY,-4,GETDATE()), 37500,  'Admin', @IdAdmin,  'DaHoan',  N'Hoàn 50% do sự cố lưới khung thành'),
    (@Ds14, DATEADD(DAY,-1,GETDATE()), 75000,  'Owner', @IdOwner1, 'DaHoan',  N'Owner hủy đơn — hoàn 100% cọc'),
    (@Ds7,  DATEADD(DAY,-1,GETDATE()), 50000,  'Admin', @IdAdmin,  'DaHoan',  N'Khiếu nại sân bẩn — bồi thường 50K'),
    (@Ds1,  DATEADD(HOUR,-2,GETDATE()),30000,  'Admin', @IdAdmin,  'DaHoan',  N'Bồi thường tiền thuê bóng xì hơi'),
    (@Ds3,  DATEADD(HOUR,-6,GETDATE()),75000,  'Admin', @IdAdmin,  'DaHoan',  N'Hoàn cọc do giữ chỗ quá lâu'),
    (@Ds9,  DATEADD(HOUR,-3,GETDATE()),195000, 'User',  @IdUser4,  'ChoXuLy', N'User yêu cầu hoàn cọc do mất điện'),
    (@Ds12, DATEADD(DAY,-2,GETDATE()), 96000,  'Owner', @IdOwner2, 'TuChoi',  N'Owner từ chối hoàn — No-show'),
    (@Ds6,  DATEADD(HOUR,-1,GETDATE()),50000,  'User',  @IdUser6,  'ChoXuLy', N'User yêu cầu hoàn 1 phần'),
    (@Ds4,  DATEADD(HOUR,-12,GETDATE()),0,     'Admin', @IdAdmin,  'TuChoi',  N'Sân đúng mô tả, không hoàn'),
    (@Ds2,  DATEADD(MINUTE,-30,GETDATE()),20000,'User', @IdUser2,  'ChoXuLy', N'Yêu cầu hoàn vì thiếu nước nóng');
PRINT N'✔ GiaoDichHoanCocs: 12';

-- ============================================================
-- BƯỚC 24: GiaiDaus (10)
-- ============================================================
INSERT INTO GiaiDaus (TenGiai, MoTa, SanBongId, OwnerId, SoDoiToiDa, SoBang, LePhiGiai, TienKyQuy,
                     TienPhatTheVang, TienPhatTheDo, SoTranTreoGioTheDo, SoTheVangTichLuy,
                     NgayBatDau, NgayKetThuc, ThoiGianTao, ThoiGianDongDanhSach, TrangThai,
                     StaffPhuTrachId, LichBlockJson)
VALUES
    (N'Giải Hè 2026 — Cầu Giấy Cup',     N'Giải đấu sân 5 hè 2026',           @San1, @IdOwner1, 8,  2, 500000,  2000000, 50000, 200000, 1, 3, DATEADD(DAY, 10, GETDATE()), DATEADD(DAY, 40, GETDATE()),  GETDATE(),                  DATEADD(DAY,  5, GETDATE()), 'DangDangKy', @IdStaff1, NULL),
    (N'Đống Đa Arena Champions League',  N'Giải sân 7 cấp khu vực',           @San2, @IdOwner1, 12, 3, 800000,  3000000, 100000,300000, 1, 3, DATEADD(DAY,  5, GETDATE()), DATEADD(DAY, 35, GETDATE()),  GETDATE(),                  DATEADD(DAY,  2, GETDATE()), 'DangDangKy', @IdStaff2, NULL),
    (N'Long Biên FIFA Open',             N'Giải sân 11 mở rộng',              @San4, @IdOwner2, 8,  2, 2000000, 5000000, 200000,500000, 2, 4, DATEADD(DAY, 20, GETDATE()), DATEADD(DAY, 60, GETDATE()),  GETDATE(),                  DATEADD(DAY, 15, GETDATE()), 'Draft',      @IdStaff3, NULL),
    (N'Nam Từ Liêm Premier Cup',         N'Giải sân 7 cao cấp',               @San5, @IdOwner2, 8,  2, 1000000, 3500000, 100000,300000, 1, 3, DATEADD(DAY, 15, GETDATE()), DATEADD(DAY, 45, GETDATE()),  GETDATE(),                  DATEADD(DAY, 10, GETDATE()), 'DangDangKy', NULL,       NULL),
    (N'Ba Đình League Mùa Xuân',         N'Giải xuân thường niên',            @San7, @IdOwner3, 6,  1, 300000,  1000000, 50000, 150000, 1, 3, DATEADD(DAY,  3, GETDATE()), DATEADD(DAY, 30, GETDATE()),  GETDATE(),                  DATEADD(DAY,  1, GETDATE()), 'DangDienRa', NULL,       NULL),
    (N'Thanh Xuân Cup 2025',             N'Giải đã kết thúc',                 @San8, @IdOwner3, 8,  2, 500000,  2000000, 50000, 200000, 1, 3, DATEADD(DAY,-60, GETDATE()), DATEADD(DAY,-10, GETDATE()),  DATEADD(DAY,-80,GETDATE()), DATEADD(DAY,-65, GETDATE()), 'DaKetThuc',  @IdStaff3, NULL),
    (N'Hà Đông Futsal Mini',             N'Giải futsal 4 đội',                @San9, @IdOwner3, 4,  1, 200000,  500000,  30000, 100000, 1, 2, DATEADD(DAY,  7, GETDATE()), DATEADD(DAY, 21, GETDATE()),  GETDATE(),                  DATEADD(DAY,  5, GETDATE()), 'DangDangKy', NULL,       NULL),
    (N'Cầu Giấy Friendly Tour',          N'Giải giao hữu cuối tuần',          @San1, @IdOwner1, 6,  1, 200000,  500000,  30000, 100000, 1, 2, DATEADD(DAY, 14, GETDATE()), DATEADD(DAY, 28, GETDATE()),  GETDATE(),                  DATEADD(DAY, 10, GETDATE()), 'Draft',      NULL,       NULL),
    (N'Đống Đa Sân 7 Master',            N'Giải đỉnh cao sân 7',              @San2, @IdOwner1, 16, 4, 1500000, 5000000, 150000,400000, 2, 3, DATEADD(DAY, 25, GETDATE()), DATEADD(DAY, 70, GETDATE()),  GETDATE(),                  DATEADD(DAY, 20, GETDATE()), 'Draft',      NULL,       NULL),
    (N'Hoàng Mai Cup Trial',             N'Giải đang chạy thử',               @San3, @IdOwner1, 4,  1, 100000,  300000,  20000, 80000,  1, 2, DATEADD(DAY,  2, GETDATE()), DATEADD(DAY, 12, GETDATE()),  GETDATE(),                  DATEADD(DAY,  1, GETDATE()), 'DangDangKy', NULL,       NULL);
PRINT N'✔ GiaiDaus: 10';

DECLARE @Gd1 INT = (SELECT Id FROM GiaiDaus WHERE TenGiai = N'Giải Hè 2026 — Cầu Giấy Cup');
DECLARE @Gd2 INT = (SELECT Id FROM GiaiDaus WHERE TenGiai = N'Đống Đa Arena Champions League');
DECLARE @Gd5 INT = (SELECT Id FROM GiaiDaus WHERE TenGiai = N'Ba Đình League Mùa Xuân');
DECLARE @Gd6 INT = (SELECT Id FROM GiaiDaus WHERE TenGiai = N'Thanh Xuân Cup 2025');

-- ============================================================
-- BƯỚC 25: BangDaus (12 — phân bố qua các giải)
-- ============================================================
INSERT INTO BangDaus (GiaiDauId, TenBang)
VALUES
    (@Gd1, N'Bảng A'),
    (@Gd1, N'Bảng B'),
    (@Gd2, N'Bảng A'),
    (@Gd2, N'Bảng B'),
    (@Gd2, N'Bảng C'),
    (@Gd5, N'Bảng Duy Nhất'),
    (@Gd6, N'Bảng A'),
    (@Gd6, N'Bảng B'),
    -- thêm cho các giải khác
    ((SELECT Id FROM GiaiDaus WHERE TenGiai = N'Long Biên FIFA Open'),         N'Bảng A'),
    ((SELECT Id FROM GiaiDaus WHERE TenGiai = N'Nam Từ Liêm Premier Cup'),      N'Bảng A'),
    ((SELECT Id FROM GiaiDaus WHERE TenGiai = N'Hà Đông Futsal Mini'),          N'Bảng Duy Nhất'),
    ((SELECT Id FROM GiaiDaus WHERE TenGiai = N'Đống Đa Sân 7 Master'),         N'Bảng A');
PRINT N'✔ BangDaus: 12';

DECLARE @Bang1A INT = (SELECT Id FROM BangDaus WHERE GiaiDauId = @Gd1 AND TenBang = N'Bảng A');
DECLARE @Bang1B INT = (SELECT Id FROM BangDaus WHERE GiaiDauId = @Gd1 AND TenBang = N'Bảng B');
DECLARE @Bang2A INT = (SELECT Id FROM BangDaus WHERE GiaiDauId = @Gd2 AND TenBang = N'Bảng A');

-- ============================================================
-- BƯỚC 26: DoiBongs (12)
-- ============================================================
INSERT INTO DoiBongs (GiaiDauId, BangId, DoiTruongId, TenDoi, LogoUrl, TienKyQuyConLai, DaThanhToan, ThoiGianThanhToan, TrangThai, ThoiGianTao)
VALUES
    (@Gd1, @Bang1A, @IdUser1, N'FC Cầu Giấy',      '/img/logos/fc_caugiay.png',    2000000, 1, DATEADD(DAY,-2,GETDATE()), 'Active',  DATEADD(DAY,-3,GETDATE())),
    (@Gd1, @Bang1A, @IdUser2, N'Tia Chớp Xanh',    '/img/logos/tia_chop.png',      2000000, 1, DATEADD(DAY,-2,GETDATE()), 'Active',  DATEADD(DAY,-3,GETDATE())),
    (@Gd1, @Bang1B, @IdUser3, N'Hổ Mang Chúa',     '/img/logos/ho_mang.png',       2000000, 1, DATEADD(DAY,-1,GETDATE()), 'Active',  DATEADD(DAY,-2,GETDATE())),
    (@Gd1, @Bang1B, @IdUser4, N'Đại Bàng Đen',     '/img/logos/dai_bang.png',      1900000, 1, DATEADD(DAY,-1,GETDATE()), 'Active',  DATEADD(DAY,-2,GETDATE())),
    (@Gd2, @Bang2A, @IdUser5, N'Đống Đa United',   '/img/logos/dongda_utd.png',    3000000, 1, DATEADD(DAY,-3,GETDATE()), 'Active',  DATEADD(DAY,-5,GETDATE())),
    (@Gd2, @Bang2A, @IdUser6, N'Tây Sơn FC',       '/img/logos/tayson_fc.png',     3000000, 1, DATEADD(DAY,-2,GETDATE()), 'Active',  DATEADD(DAY,-4,GETDATE())),
    (@Gd2, NULL,    @IdUser7, N'Ngôi Sao Mới',     NULL,                            0,       0, NULL,                      'ChoDuyet',DATEADD(DAY,-1,GETDATE())),
    (@Gd5, (SELECT Id FROM BangDaus WHERE GiaiDauId = @Gd5), @IdUser8, N'Ba Đình Boys', NULL,           1000000, 1, GETDATE(),                 'Active',  DATEADD(DAY,-1,GETDATE())),
    (@Gd6, (SELECT TOP 1 Id FROM BangDaus WHERE GiaiDauId = @Gd6), @IdUser1, N'Thanh Xuân Veterans', NULL, 2000000, 1, DATEADD(DAY,-60,GETDATE()), 'DaLoai', DATEADD(DAY,-65,GETDATE())),
    (@Gd6, (SELECT TOP 1 Id FROM BangDaus WHERE GiaiDauId = @Gd6), @IdUser2, N'Sư Tử Vàng',       NULL,    2000000, 1, DATEADD(DAY,-60,GETDATE()), 'VoDich', DATEADD(DAY,-65,GETDATE())),
    ((SELECT Id FROM GiaiDaus WHERE TenGiai = N'Hà Đông Futsal Mini'), NULL,@IdUser3, N'Futsal Friends', NULL, 500000, 0, NULL,                      'ChoDuyet',DATEADD(DAY,-1,GETDATE())),
    ((SELECT Id FROM GiaiDaus WHERE TenGiai = N'Long Biên FIFA Open'),NULL, @IdUser4, N'Real Long Biên', '/img/logos/real_lb.png', 5000000, 1, DATEADD(DAY,-2,GETDATE()), 'Active', DATEADD(DAY,-4,GETDATE()));
PRINT N'✔ DoiBongs: 12';

DECLARE @Doi1 INT = (SELECT Id FROM DoiBongs WHERE TenDoi = N'FC Cầu Giấy');
DECLARE @Doi2 INT = (SELECT Id FROM DoiBongs WHERE TenDoi = N'Tia Chớp Xanh');
DECLARE @Doi3 INT = (SELECT Id FROM DoiBongs WHERE TenDoi = N'Hổ Mang Chúa');
DECLARE @Doi4 INT = (SELECT Id FROM DoiBongs WHERE TenDoi = N'Đại Bàng Đen');
DECLARE @Doi5 INT = (SELECT Id FROM DoiBongs WHERE TenDoi = N'Đống Đa United');
DECLARE @Doi6 INT = (SELECT Id FROM DoiBongs WHERE TenDoi = N'Tây Sơn FC');

-- ============================================================
-- BƯỚC 27: ThanhVienDoi (15 — UNIQUE(DoiId, SoAo))
-- ============================================================
INSERT INTO ThanhVienDois (DoiId, HoTen, SoAo, AnhDaiDien, SoTranTreoGio, TongBanThang, TongTheVang, TongTheDo)
VALUES
    (@Doi1, N'Nguyễn Công Nam',  10, NULL, 0, 5, 1, 0),
    (@Doi1, N'Phạm Văn A',        7, NULL, 0, 2, 0, 0),
    (@Doi1, N'Lê Văn B',          1, NULL, 0, 0, 0, 0),
    (@Doi2, N'Trần Quốc Bảo',     9, NULL, 0, 4, 2, 0),
    (@Doi2, N'Hoàng Văn C',       3, NULL, 1, 1, 3, 1),
    (@Doi3, N'Đỗ Minh Quân',      8, NULL, 0, 3, 0, 0),
    (@Doi3, N'Nguyễn Văn D',      4, NULL, 0, 0, 1, 0),
    (@Doi4, N'Hoàng Thị Lan',     11, NULL, 0, 2, 0, 0),
    (@Doi4, N'Bùi Văn E',         6, NULL, 0, 1, 1, 0),
    (@Doi5, N'Bùi Văn Hậu',       5, NULL, 0, 6, 1, 0),
    (@Doi5, N'Vũ Văn F',          2, NULL, 0, 3, 0, 0),
    (@Doi6, N'Dương Thu Trang',   10, NULL, 0, 4, 1, 0),
    (@Doi6, N'Đỗ Văn G',          8, NULL, 0, 2, 0, 0),
    (@Doi6, N'Nguyễn Văn H',      1, NULL, 0, 0, 0, 0),
    (@Doi3, N'Lê Văn I',         12, NULL, 0, 1, 2, 0);
PRINT N'✔ ThanhVienDois: 15';

DECLARE @Tv1_Nam INT = (SELECT Id FROM ThanhVienDois WHERE DoiId = @Doi1 AND SoAo = 10);
DECLARE @Tv2_Bao INT = (SELECT Id FROM ThanhVienDois WHERE DoiId = @Doi2 AND SoAo = 9);
DECLARE @Tv5_Hau INT = (SELECT Id FROM ThanhVienDois WHERE DoiId = @Doi5 AND SoAo = 5);
DECLARE @Tv6_Trang INT = (SELECT Id FROM ThanhVienDois WHERE DoiId = @Doi6 AND SoAo = 10);

-- ============================================================
-- BƯỚC 28: TranDaus (12)
-- ============================================================
INSERT INTO TranDaus (GiaiDauId, BangId, KhungGioId, DoiNhaId, DoiKhachId, BanThangNha, BanThangKhach,
                     VongDau, LoaiVong, NgayThiDau, TrangThai, StaffPhuTrachId)
VALUES
    -- Giải 1 (chưa diễn ra)
    (@Gd1, @Bang1A, @KG1_Trong,  @Doi1, @Doi2, NULL, NULL, 1, 'VongBang',   DATEADD(DAY, 11, GETDATE()), 'Scheduled', @IdStaff1),
    (@Gd1, @Bang1A, @KG1_Trong,  @Doi1, @Doi2, NULL, NULL, 2, 'VongBang',   DATEADD(DAY, 18, GETDATE()), 'Scheduled', @IdStaff1),
    (@Gd1, @Bang1B, @KG1_Trong,  @Doi3, @Doi4, NULL, NULL, 1, 'VongBang',   DATEADD(DAY, 11, GETDATE()), 'Scheduled', @IdStaff1),
    (@Gd1, @Bang1B, @KG1_Trong,  @Doi3, @Doi4, NULL, NULL, 2, 'VongBang',   DATEADD(DAY, 18, GETDATE()), 'Scheduled', @IdStaff1),
    (@Gd1, NULL,    @KG1_Trong,  @Doi1, @Doi3, NULL, NULL, 3, 'BanKet',     DATEADD(DAY, 25, GETDATE()), 'Scheduled', @IdStaff1),
    (@Gd1, NULL,    @KG1_Trong,  @Doi2, @Doi4, NULL, NULL, 3, 'BanKet',     DATEADD(DAY, 25, GETDATE()), 'Scheduled', @IdStaff1),
    (@Gd1, NULL,    @KG1_Trong,  @Doi1, @Doi2, NULL, NULL, 4, 'ChungKet',   DATEADD(DAY, 30, GETDATE()), 'Scheduled', @IdStaff1),
    -- Giải 2 (đã diễn ra một số trận)
    (@Gd2, @Bang2A, @KG2_Trong,  @Doi5, @Doi6, 3, 2,    1, 'VongBang',   DATEADD(DAY, -2, GETDATE()), 'KetThuc',   @IdStaff2),
    (@Gd2, @Bang2A, @KG2_Trong,  @Doi5, @Doi6, 1, 1,    2, 'VongBang',   DATEADD(DAY,  4, GETDATE()), 'Scheduled', @IdStaff2),
    -- Giải 5 đang diễn ra
    (@Gd5, NULL,    @KG1_Trong,  @Doi1, @Doi3, 2, 1,    1, 'VongBang',   DATEADD(HOUR,-2,GETDATE()), 'DangDien',  NULL),
    -- Giải 6 đã kết thúc
    (@Gd6, NULL,    NULL,        @Doi5, @Doi6, 4, 2,    7, 'ChungKet',   DATEADD(DAY,-12,GETDATE()), 'KetThuc',   @IdStaff3),
    (@Gd6, NULL,    NULL,        @Doi1, @Doi2, 0, 3,    7, 'TranhBaTu',  DATEADD(DAY,-12,GETDATE()), 'KetThuc',   @IdStaff3);
PRINT N'✔ TranDaus: 12';

DECLARE @Tran1 INT = (SELECT TOP 1 Id FROM TranDaus WHERE GiaiDauId = @Gd2 AND TrangThai = 'KetThuc');
DECLARE @Tran2 INT = (SELECT TOP 1 Id FROM TranDaus WHERE GiaiDauId = @Gd5 AND TrangThai = 'DangDien');
DECLARE @Tran3 INT = (SELECT TOP 1 Id FROM TranDaus WHERE GiaiDauId = @Gd6 AND LoaiVong = 'ChungKet');

-- ============================================================
-- BƯỚC 29: SuKienTran (15)
-- ============================================================
INSERT INTO SuKienTrans (TranDauId, ThanhVienId, DoiId, LoaiSuKien, Phut, GhiChu, ThoiGianGhi)
VALUES
    (@Tran1, @Tv5_Hau,   @Doi5, N'BanThang',   12, N'Sút xa đẹp mắt',                  DATEADD(DAY,-2,GETDATE())),
    (@Tran1, @Tv5_Hau,   @Doi5, N'BanThang',   34, N'Đánh đầu cận thành',              DATEADD(DAY,-2,GETDATE())),
    (@Tran1, @Tv6_Trang, @Doi6, N'BanThang',   45, N'Phản công nhanh',                 DATEADD(DAY,-2,GETDATE())),
    (@Tran1, @Tv5_Hau,   @Doi5, N'BanThang',   78, N'Phạt đền',                        DATEADD(DAY,-2,GETDATE())),
    (@Tran1, @Tv6_Trang, @Doi6, N'BanThang',   85, N'Đá phạt thành bàn',               DATEADD(DAY,-2,GETDATE())),
    (@Tran1, @Tv5_Hau,   @Doi5, N'TheVang',    60, N'Phạm lỗi chiến thuật',            DATEADD(DAY,-2,GETDATE())),
    (@Tran1, @Tv6_Trang, @Doi6, N'TheVang',    72, N'Cản phá trái phép',               DATEADD(DAY,-2,GETDATE())),
    (@Tran1, NULL,       @Doi5, N'ThayNguoi',  55, N'Thay người chiến thuật',          DATEADD(DAY,-2,GETDATE())),
    (@Tran1, NULL,       @Doi6, N'ThayNguoi',  65, N'Tiền đạo dự bị vào sân',          DATEADD(DAY,-2,GETDATE())),
    -- Trận đang diễn ra
    (@Tran2, @Tv1_Nam,   @Doi1, N'BanThang',   15, N'Mở tỉ số',                        GETDATE()),
    (@Tran2, NULL,       @Doi3, N'BanThang',   28, N'Gỡ hòa từ phản công',             GETDATE()),
    (@Tran2, @Tv1_Nam,   @Doi1, N'BanThang',   42, N'Đệm bóng cận thành',              GETDATE()),
    -- Trận chung kết Giải 6
    (@Tran3, @Tv5_Hau,   @Doi5, N'BanThang',   10, N'Sút xa mở tỉ số',                 DATEADD(DAY,-12,GETDATE())),
    (@Tran3, @Tv6_Trang, @Doi6, N'TheDo',      55, N'Đốn ngã đối phương',              DATEADD(DAY,-12,GETDATE())),
    (@Tran3, NULL,       @Doi5, N'KetThuc',  NULL, N'Sư Tử Vàng vô địch 4-2',          DATEADD(DAY,-12,GETDATE()));
PRINT N'✔ SuKienTran: 15';

-- ============================================================
-- BƯỚC 30: YeuCauDoiGios (12)
-- ============================================================
INSERT INTO YeuCauDoiGios (DatSanId, KhungGioMoiId, NgayThiDauMoi, LyDo, TrangThai, NgayTao, NgayXuLy, NguoiXuLyId, GhiChuXuLy)
VALUES
    (@Ds1,  @KG1_Trong, DATEADD(DAY, 3, GETDATE()), N'Bận đột xuất chiều mai',                    'ChoPheDuyet', GETDATE(),                  NULL,                          NULL,      NULL),
    (@Ds2,  @KG2_Trong, DATEADD(DAY, 5, GETDATE()), N'Đội bạn không đủ người',                    'ChoPheDuyet', GETDATE(),                  NULL,                          NULL,      NULL),
    (@Ds3,  @KG1_Trong, DATEADD(DAY, 6, GETDATE()), N'Trời mưa buổi sáng',                         'DaPheDuyet',  DATEADD(DAY,-1,GETDATE()),  DATEADD(HOUR,-2,GETDATE()),    @IdOwner1, N'OK, đổi khung giờ buổi tối'),
    (@Ds4,  @KG2_Trong, DATEADD(DAY, 7, GETDATE()), N'Không tới được vì kẹt xe',                   'TuChoi',      DATEADD(DAY,-1,GETDATE()),  DATEADD(HOUR,-1,GETDATE()),    @IdOwner1, N'Khung giờ mới đã có người đặt'),
    (@Ds5,  @KG4_Trong, DATEADD(DAY, 8, GETDATE()), N'Đổi sang đá tối cho mát',                    'ChoPheDuyet', DATEADD(HOUR,-3,GETDATE()), NULL,                          NULL,      NULL),
    (@Ds6,  @KG5_Trong, DATEADD(DAY, 1, GETDATE()), N'Trùng lịch họp công ty',                     'ChoPheDuyet', DATEADD(HOUR,-2,GETDATE()), NULL,                          NULL,      NULL),
    (@Ds7,  @KG1_Trong, DATEADD(DAY, 4, GETDATE()), N'Nhỡ lịch (đơn lịch sử để test)',             'DaPheDuyet',  DATEADD(DAY,-7,GETDATE()),  DATEADD(DAY,-5,GETDATE()),     @IdOwner1, N'Đã đổi khung giờ thành công'),
    (@Ds8,  @KG2_Trong, DATEADD(DAY, 2, GETDATE()), N'Thử nghiệm yêu cầu đổi giờ',                 'TuChoi',      DATEADD(DAY,-2,GETDATE()),  DATEADD(DAY,-1,GETDATE()),     @IdOwner1, N'Quá thời gian cho phép đổi'),
    (@Ds9,  @KG4_Trong, DATEADD(DAY, 9, GETDATE()), N'Đội bạn yêu cầu đổi qua tối cuối tuần',      'ChoPheDuyet', DATEADD(MINUTE,-30,GETDATE()),NULL,                        NULL,      NULL),
    (@Ds10, @KG1_Trong, DATEADD(DAY, 5, GETDATE()), N'Yêu cầu mở lại đơn đã hủy (test edge case)', 'TuChoi',      DATEADD(DAY,-6,GETDATE()),  DATEADD(DAY,-5,GETDATE()),     @IdOwner1, N'Đơn đã hủy không thể mở lại'),
    (@Ds13, @KG1_Trong, DATEADD(DAY, 6, GETDATE()), N'Đổi giờ cho buổi đá bù',                     'DaPheDuyet',  DATEADD(DAY,-14,GETDATE()), DATEADD(DAY,-13,GETDATE()),    @IdOwner1, N'OK'),
    (@Ds15, @KG2_Trong, DATEADD(DAY, 9, GETDATE()), N'Đổi sang sáng cuối tuần',                    'ChoPheDuyet', DATEADD(MINUTE,-10,GETDATE()),NULL,                        NULL,      NULL);
PRINT N'✔ YeuCauDoiGios: 12';

-- ============================================================
-- BƯỚC 31: YeuCauDoiSans (12)
-- ============================================================
INSERT INTO YeuCauDoiSans (DatSanId, KhungGioMoiId, NgayThiDauMoi, LyDo, TrangThai, NgayTao, NgayXuLy, NguoiXuLyId, GhiChuXuLy)
VALUES
    (@Ds1,  @KG2_Trong, DATEADD(DAY, 3, GETDATE()), N'Đổi sang sân 7 cho đông người hơn',        'ChoPheDuyet', GETDATE(),                  NULL,                       NULL,      NULL),
    (@Ds2,  @KG4_Trong, DATEADD(DAY, 5, GETDATE()), N'Đổi sang sân 11 vì đủ người',              'ChoPheDuyet', GETDATE(),                  NULL,                       NULL,      NULL),
    (@Ds3,  @KG2_Trong, DATEADD(DAY, 6, GETDATE()), N'Sân 5 nhỏ quá, đổi sân 7',                  'DaPheDuyet',  DATEADD(DAY,-1,GETDATE()),  DATEADD(HOUR,-3,GETDATE()), @IdOwner1, N'OK, đã đổi sân'),
    (@Ds4,  @KG1_Trong, DATEADD(DAY, 5, GETDATE()), N'Đổi sang sân khác cho tiện đường',          'TuChoi',      DATEADD(DAY,-1,GETDATE()),  DATEADD(HOUR,-2,GETDATE()), @IdOwner2, N'Sân yêu cầu thuộc Owner khác'),
    (@Ds5,  @KG5_Trong, DATEADD(DAY, 8, GETDATE()), N'Sân 11 mưa lầy, đổi sân nhân tạo',          'ChoPheDuyet', DATEADD(HOUR,-2,GETDATE()), NULL,                       NULL,      NULL),
    (@Ds6,  @KG1_Trong, DATEADD(DAY, 1, GETDATE()), N'Đổi sang sân gần nhà hơn',                  'ChoPheDuyet', DATEADD(HOUR,-1,GETDATE()), NULL,                       NULL,      NULL),
    (@Ds7,  @KG2_Trong, DATEADD(DAY,-2, GETDATE()), N'(Lịch sử) Đổi sân thành công',              'DaPheDuyet',  DATEADD(DAY,-5,GETDATE()),  DATEADD(DAY,-4,GETDATE()),  @IdOwner1, N'Đã đổi sân'),
    (@Ds8,  @KG4_Trong, DATEADD(DAY,-3, GETDATE()), N'(Lịch sử) Test edge case',                  'TuChoi',      DATEADD(DAY,-6,GETDATE()),  DATEADD(DAY,-5,GETDATE()),  @IdOwner2, N'Sân không trống'),
    (@Ds9,  @KG2_Trong, DATEADD(DAY, 9, GETDATE()), N'Đổi sang sân 7 sang trọng hơn',             'ChoPheDuyet', DATEADD(MINUTE,-15,GETDATE()),NULL,                     NULL,      NULL),
    (@Ds11, @KG5_Trong, DATEADD(DAY, 1, GETDATE()), N'Yêu cầu sau khi đã hủy đơn',                'TuChoi',      DATEADD(DAY,-1,GETDATE()),  DATEADD(HOUR,-12,GETDATE()),@IdOwner2, N'Đơn đã hủy không thể đổi sân'),
    (@Ds13, @KG2_Trong, DATEADD(DAY, 6, GETDATE()), N'Đổi sân do hỏng hóc khắc phục',             'DaPheDuyet',  DATEADD(DAY,-14,GETDATE()), DATEADD(DAY,-13,GETDATE()), @IdOwner1, N'OK'),
    (@Ds15, @KG1_Trong, DATEADD(DAY, 9, GETDATE()), N'Đổi sang sân 5 ít người',                   'ChoPheDuyet', DATEADD(MINUTE,-5,GETDATE()), NULL,                     NULL,      NULL);
PRINT N'✔ YeuCauDoiSans: 12';

-- ============================================================
-- BƯỚC 32: ChuyenNhuongDatSans (12)
-- ============================================================
INSERT INTO ChuyenNhuongDatSans (DatSanId, NguoiChuyenId, EmailNguoiNhan, SdtNguoiNhan, NguoiNhanId, LyDo, TrangThai, NgayTao, NgayXuLy, NguoiXuLyOwnerId, GhiChuXuLy)
VALUES
    (@Ds1,  @IdUser1, 'user2@gmail.com', '0901000006', @IdUser2, N'Bận đột xuất, chuyển cho bạn',                  'ChoPheDuyet', GETDATE(),                  NULL,                          NULL,       NULL),
    (@Ds2,  @IdUser2, 'user4@gmail.com', '0901000008', @IdUser4, N'Đội mình hủy, chuyển cho đội khác',              'ChoPheDuyet', GETDATE(),                  NULL,                          NULL,       NULL),
    (@Ds3,  @IdUser3, 'user5@gmail.com', '0901000009', @IdUser5, N'Không tới được, chuyển cho đối tác',             'DaPheDuyet',  DATEADD(DAY,-1,GETDATE()),  DATEADD(HOUR,-3,GETDATE()),    @IdOwner1,  N'OK chuyển nhượng thành công'),
    (@Ds4,  @IdUser4, 'newuser@gmail.com','0903000111',NULL,      N'Chuyển cho người mới (chưa có account)',         'ChoPheDuyet', DATEADD(HOUR,-6,GETDATE()), NULL,                          NULL,       NULL),
    (@Ds5,  @IdUser5, 'user1@gmail.com', '0901000005', @IdUser1, N'Chuyển nhượng vì đột xuất công tác',             'TuChoi',      DATEADD(DAY,-1,GETDATE()),  DATEADD(HOUR,-2,GETDATE()),    @IdOwner2,  N'Người nhận đã có đơn cùng giờ'),
    (@Ds6,  @IdUser6, 'user7@gmail.com', '0901000011', @IdUser7, N'Hết bận, nhường cho bạn',                         'ChoPheDuyet', DATEADD(HOUR,-3,GETDATE()), NULL,                          NULL,       NULL),
    (@Ds7,  @IdUser1, 'user3@gmail.com', '0901000007', @IdUser3, N'(Lịch sử) đã chuyển nhượng',                      'DaPheDuyet',  DATEADD(DAY,-7,GETDATE()),  DATEADD(DAY,-6,GETDATE()),     @IdOwner1,  N'Đã hoàn tất'),
    (@Ds8,  @IdUser2, 'user6@gmail.com', '0901000010', @IdUser6, N'(Lịch sử) chuyển nhượng',                          'DaPheDuyet',  DATEADD(DAY,-9,GETDATE()),  DATEADD(DAY,-8,GETDATE()),     @IdOwner1,  N'Đã hoàn tất'),
    (@Ds9,  @IdUser4, 'user8@gmail.com', '0901000012', @IdUser8, N'Chuyển nhượng cho đối tác do hủy hợp đồng',        'ChoPheDuyet', DATEADD(MINUTE,-30,GETDATE()),NULL,                        NULL,       NULL),
    (@Ds11, @IdUser8, 'user1@gmail.com', '0901000005', @IdUser1, N'Yêu cầu chuyển nhượng đơn đã hủy (edge case)',     'TuChoi',      DATEADD(DAY,-1,GETDATE()),  DATEADD(HOUR,-12,GETDATE()),   @IdOwner1,  N'Đơn đã hủy không thể chuyển'),
    (@Ds13, @IdUser3, 'user4@gmail.com', '0901000008', @IdUser4, N'Chuyển nhượng sau sự cố hỏng hóc',                 'DaPheDuyet',  DATEADD(DAY,-12,GETDATE()), DATEADD(DAY,-11,GETDATE()),    @IdOwner1,  N'Đã hoàn tất sau khi sửa sân'),
    (@Ds15, @IdUser7, 'user5@gmail.com', '0901000009', @IdUser5, N'Chuyển nhượng cuối tuần',                          'ChoPheDuyet', DATEADD(MINUTE,-10,GETDATE()),NULL,                        NULL,       NULL);
PRINT N'✔ ChuyenNhuongDatSans: 12';

-- ============================================================
-- COMMIT TRANSACTION
-- ============================================================
COMMIT TRANSACTION;
GO

PRINT N'';
PRINT N'====================================================';
PRINT N'  ✅ SEED DATA HOÀN TẤT — Tất cả các bảng đã đầy dữ liệu';
PRINT N'====================================================';
PRINT N'  Tài khoản test:';
PRINT N'    admin@pitchhub.vn   → admin123';
PRINT N'    owner1@gmail.com    → owner123';
PRINT N'    owner2@gmail.com    → owner123';
PRINT N'    owner3@gmail.com    → owner123';
PRINT N'    staff1@pitchhub.vn  → staff123 (của owner1)';
PRINT N'    staff2@pitchhub.vn  → staff123 (của owner1)';
PRINT N'    staff3@pitchhub.vn  → staff123 (của owner2)';
PRINT N'    user1..user8@gmail.com → user123';
PRINT N'====================================================';
GO
