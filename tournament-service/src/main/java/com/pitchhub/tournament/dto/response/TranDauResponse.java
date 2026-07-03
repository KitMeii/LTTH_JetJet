package com.pitchhub.tournament.dto.response;

import com.pitchhub.tournament.model.TranDau;
import lombok.Getter;
import lombok.Setter;
import lombok.NoArgsConstructor;
import org.hibernate.Hibernate;

import java.math.BigDecimal;
import java.time.LocalDateTime;
import java.util.List;

@Getter
@Setter
@NoArgsConstructor
public class TranDauResponse {
    private Integer id;
    private Integer giaiDauId;
    private Integer bangId;
    private String tenBang;
    private Integer doiNhaId;
    private String tenDoiNha;
    private Integer doiKhachId;
    private String tenDoiKhach;
    private Integer banThangNha;
    private Integer banThangKhach;
    private Integer vongDau;
    private String loaiVong;
    private LocalDateTime ngayThiDau;
    private String trangThai;
    private Integer staffPhuTrachId;

    // Chi dien khi goi tu TournamentStaffController (can cho man hinh ghi
    // su kien/check-in chon cau thu) — null o cac endpoint danh sach thong thuong.
    private List<ThanhVienDoiResponse> doiNhaThanhViens;
    private List<ThanhVienDoiResponse> doiKhachThanhViens;

    // Cac field "enrich" bo sung — controller tu dien sau khi goi from(),
    // giong pattern enrich() cua GiaiDauResponse (tranh phai JOIN qua nhieu bang).
    private String staffPhuTrachHoTen;
    private String giaiTenGiai;
    private BigDecimal giaiTienPhatTheVang;
    private BigDecimal giaiTienPhatTheDo;
    private String sanBongTenSan;

    public static TranDauResponse from(TranDau t) {
        TranDauResponse r = new TranDauResponse();
        r.id = t.getId();
        r.giaiDauId = t.getGiaiDauId();
        r.bangId = t.getBangId();
        r.tenBang = t.getBangDau() != null ? t.getBangDau().getTenBang() : null;
        r.banThangNha = t.getBanThangNha();
        r.banThangKhach = t.getBanThangKhach();
        r.vongDau = t.getVongDau();
        r.loaiVong = t.getLoaiVong();
        r.ngayThiDau = t.getNgayThiDau();
        r.trangThai = t.getTrangThai();
        r.staffPhuTrachId = t.getStaffPhuTrachId();

        // "0" la sentinel TBD (DB NOT NULL DEFAULT 0 — xem @DynamicInsert tren
        // TranDau) cho tran knock-out placeholder chua co doi. Phai kiem tra qua
        // cot tho (doiNhaId/doiKhachId) TRUOC — cham vao t.getDoiNha() khi id=0
        // se khoi tao proxy toi mot DoiBong khong ton tai va nem EntityNotFoundException.
        boolean coDoiNha = t.getDoiNhaId() != null && t.getDoiNhaId() != 0;
        boolean coDoiKhach = t.getDoiKhachId() != null && t.getDoiKhachId() != 0;
        r.doiNhaId = coDoiNha ? t.getDoiNhaId() : null;
        r.doiKhachId = coDoiKhach ? t.getDoiKhachId() : null;
        r.tenDoiNha = coDoiNha && t.getDoiNha() != null ? t.getDoiNha().getTenDoi() : null;
        r.tenDoiKhach = coDoiKhach && t.getDoiKhach() != null ? t.getDoiKhach().getTenDoi() : null;

        // isInitialized guard: cac query "danh sach" (findByGiaiDauId/findChoStaff...)
        // khong fetch thanhViens, chi findByIdWithGiaiDau moi fetch — doc truc tiep se
        // nem LazyInitializationException vi controller khong mo transaction.
        if (coDoiNha && t.getDoiNha() != null && Hibernate.isInitialized(t.getDoiNha().getThanhViens())) {
            r.doiNhaThanhViens = t.getDoiNha().getThanhViens().stream().map(ThanhVienDoiResponse::from).toList();
        }
        if (coDoiKhach && t.getDoiKhach() != null && Hibernate.isInitialized(t.getDoiKhach().getThanhViens())) {
            r.doiKhachThanhViens = t.getDoiKhach().getThanhViens().stream().map(ThanhVienDoiResponse::from).toList();
        }
        return r;
    }
}
