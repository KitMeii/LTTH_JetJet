-- ============================================================
--  FIX nhanh password cho các user đã seed (không cần re-seed)
--  Mật khẩu sau khi chạy:
--    Admin  → admin123
--    Owner  → owner123
--    Staff  → staff123
--    User   → user123
--  (Các hash bên dưới là BCrypt thực của các chuỗi tương ứng,
--   được lấy từ file SanBongBTL.sql gốc và đã verify ok.)
-- ============================================================

USE SanBongBTL;
GO

-- Admin
UPDATE Users
SET MatKhau = '$2a$11$f0tXD6o7XYAs7/tE0nx4Reiw1.84L2ItgL0tRwE1Bq.GZbE8MMuzS'   -- admin123
WHERE VaiTro = 'Admin';

-- Owner
UPDATE Users
SET MatKhau = '$2a$11$dZvxGdl0dNQWsvIM4IO2VuM4kGwP60qFmpbIOKvD0iLulA00/4cCW'   -- owner123
WHERE VaiTro = 'Owner';

-- Staff
UPDATE Users
SET MatKhau = '$2a$11$oOUcIEMbESDcI5QECnfcBOzJtTAAsWbRM3ZX7KQhH3XIWWdmtfR1S'   -- staff123
WHERE VaiTro = 'Staff';

-- User
UPDATE Users
SET MatKhau = '$2a$11$wmJXgVs5/RBXU2y/vP4Bs.UcgtzPg0r4Iv2t3DgsZvDrOKD32vPzO'   -- user123
WHERE VaiTro = 'User';

PRINT N'';
PRINT N'====================================================';
PRINT N'  ✅ Đã cập nhật mật khẩu cho tất cả Users';
PRINT N'====================================================';
PRINT N'  Tài khoản test:';
PRINT N'    admin@pitchhub.vn   → admin123';
PRINT N'    owner1@gmail.com    → owner123';
PRINT N'    owner2@gmail.com    → owner123';
PRINT N'    owner3@gmail.com    → owner123';
PRINT N'    staff1@pitchhub.vn  → staff123';
PRINT N'    staff2@pitchhub.vn  → staff123';
PRINT N'    staff3@pitchhub.vn  → staff123';
PRINT N'    user1..user8@gmail.com → user123';
PRINT N'====================================================';
GO
