package com.pitchhub.checkin.service;

import com.pitchhub.checkin.dto.BookingInfo;
import com.pitchhub.checkin.dto.CheckInResult;
import org.springframework.beans.factory.annotation.Autowired;
import org.springframework.dao.EmptyResultDataAccessException;
import org.springframework.jdbc.core.JdbcTemplate;
import org.springframework.jdbc.core.RowMapper;
import org.springframework.stereotype.Service;
import org.springframework.transaction.annotation.Transactional;

import java.sql.ResultSet;
import java.sql.SQLException;
import java.sql.Timestamp;
import java.sql.Time;
import java.time.LocalDateTime;

@Service
public class CheckInService {

    private static final String SELECT_BOOKING_SQL = """
        SELECT
            d.Id            AS DatSanId,
            d.MaXacNhan     AS MaXacNhan,
            d.TrangThai     AS TrangThai,
            d.NgayThiDau    AS NgayThiDau,
            d.TienCoc       AS TienCoc,
            d.TongTien      AS TongTien,
            u.HoTen         AS HoTen,
            u.SoDienThoai   AS SoDienThoai,
            u.Email         AS Email,
            s.Id            AS SanBongId,
            s.TenSan        AS TenSan,
            s.OwnerId       AS OwnerId,
            k.GioBatDau     AS GioBatDau,
            k.GioKetThuc    AS GioKetThuc,
            k.Gia           AS Gia
        FROM DatSans d
        INNER JOIN Users u        ON u.Id = d.UserId
        INNER JOIN KhungGios k    ON k.Id = d.KhungGioId
        INNER JOIN SanBongs s     ON s.Id = k.SanBongId
        WHERE d.MaXacNhan = ?
        """;

    private final RowMapper<BookingInfo> mapper = (ResultSet rs, int i) -> {
        BookingInfo b = new BookingInfo();
        b.setDatSanId(rs.getInt("DatSanId"));
        b.setMaXacNhan(rs.getString("MaXacNhan"));
        b.setTrangThai(rs.getString("TrangThai"));

        Timestamp ts = rs.getTimestamp("NgayThiDau");
        if (ts != null) b.setNgayThiDau(ts.toLocalDateTime());

        b.setTienCoc(rs.getBigDecimal("TienCoc"));
        b.setTongTien(rs.getBigDecimal("TongTien"));
        b.setHoTen(rs.getString("HoTen"));
        b.setSoDienThoai(rs.getString("SoDienThoai"));
        b.setEmail(rs.getString("Email"));
        b.setSanBongId(rs.getInt("SanBongId"));
        b.setTenSan(rs.getString("TenSan"));
        b.setOwnerId(rs.getInt("OwnerId"));

        Time gbd = rs.getTime("GioBatDau");
        if (gbd != null) b.setGioBatDau(gbd.toLocalTime());
        Time gkt = rs.getTime("GioKetThuc");
        if (gkt != null) b.setGioKetThuc(gkt.toLocalTime());

        b.setGiaKhungGio(rs.getBigDecimal("Gia"));
        return b;
    };

    @Autowired
    private JdbcTemplate jdbc;

    public BookingInfo findByMaXacNhan(String maXacNhan) {
        if (maXacNhan == null || maXacNhan.isBlank()) return null;
        try {
            return jdbc.queryForObject(SELECT_BOOKING_SQL, mapper, maXacNhan.trim());
        } catch (EmptyResultDataAccessException e) {
            return null;
        }
    }

    public CheckInResult lookup(String maXacNhan, Integer ownerId) {
        BookingInfo b = findByMaXacNhan(maXacNhan);
        if (b == null) return CheckInResult.fail("Không tìm thấy đơn với mã \"" + maXacNhan + "\".");
        if (ownerId != null && !ownerId.equals(b.getOwnerId())) {
            return CheckInResult.fail("Đơn này không thuộc sân của bạn.");
        }
        return CheckInResult.ok("OK", b);
    }

    @Transactional
    public CheckInResult performCheckIn(String maXacNhan, Integer ownerId) {
        if (ownerId == null) return CheckInResult.fail("Thiếu ownerId.");

        BookingInfo b = findByMaXacNhan(maXacNhan);
        if (b == null) return CheckInResult.fail("Không tìm thấy đơn với mã \"" + maXacNhan + "\".");

        if (!ownerId.equals(b.getOwnerId())) {
            return CheckInResult.fail("Đơn này không thuộc sân của bạn.");
        }

        String tt = b.getTrangThai();
        switch (tt) {
            case "DaXacNhan":
                break;
            case "ChoDuyet":
                return CheckInResult.fail("Đơn đang chờ duyệt — chưa thể check-in.", b);
            case "DaHuy":
                return CheckInResult.fail("Đơn đã bị hủy — không thể check-in.", b);
            case "DangSuDung":
                return CheckInResult.fail("Đơn đã được check-in trước đó.", b);
            case "HoanThanh":
                return CheckInResult.fail("Đơn đã hoàn thành.", b);
            default:
                return CheckInResult.fail("Trạng thái \"" + tt + "\" không cho phép check-in.", b);
        }

        int rows = jdbc.update(
            "UPDATE DatSans SET TrangThai = 'DangSuDung', StaffCheckInId = ? WHERE Id = ? AND TrangThai = 'DaXacNhan'",
            ownerId, b.getDatSanId()
        );

        if (rows == 0) {
            return CheckInResult.fail("Check-in thất bại — trạng thái đã thay đổi. Vui lòng làm mới.", b);
        }

        jdbc.update(
            "INSERT INTO AuditLogs (UserId, VaiTro, HanhDong, DoiTuong, DoiTuongId, MoTa, ThoiGian) " +
            "VALUES (?, 'Owner', 'CheckIn', 'DatSan', ?, ?, ?)",
            ownerId,
            b.getDatSanId(),
            "QR Check-in đơn " + b.getMaXacNhan() + " — " + b.getHoTen(),
            Timestamp.valueOf(LocalDateTime.now())
        );

        b.setTrangThai("DangSuDung");
        return CheckInResult.ok(
            "✅ Check-in thành công: " + b.getHoTen() + " — " + b.getTenSan(),
            b
        );
    }
}
