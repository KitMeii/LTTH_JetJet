package com.pitchhub.tournament.repository;

import com.pitchhub.tournament.model.TranDau;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;
import org.springframework.data.repository.query.Param;

import java.time.LocalDateTime;
import java.util.List;
import java.util.Optional;

public interface TranDauRepository extends JpaRepository<TranDau, Integer> {

    // Dung cho TournamentStaffController: can giaiDau (de kiem tra quyen theo
    // san) + bangDau/doiNha/doiKhach deu eager, ke ca ThanhViens cua 2 doi
    // (man hinh ghi su kien/check-in cua Staff can chon cau thu tu roster).
    @Query("select t from TranDau t "
            + "join fetch t.giaiDau "
            + "left join fetch t.bangDau "
            + "left join fetch t.doiNha dn left join fetch dn.thanhViens "
            + "left join fetch t.doiKhach dk left join fetch dk.thanhViens "
            + "where t.id = :id")
    Optional<TranDau> findByIdWithGiaiDau(@Param("id") Integer id);

    // LEFT JOIN FETCH bangDau/doiNha/doiKhach — TranDauResponse.from() doc cac
    // navigation nay ngay sau khi tra ve, ma controller khong mo transaction
    // (open-in-view=false) nen phai fetch san, tranh LazyInitializationException.
    @Query("select t from TranDau t "
            + "left join fetch t.bangDau left join fetch t.doiNha left join fetch t.doiKhach "
            + "where t.giaiDauId = :giaiDauId")
    List<TranDau> findByGiaiDauId(@Param("giaiDauId") Integer giaiDauId);

    List<TranDau> findByGiaiDauIdAndTrangThai(Integer giaiDauId, String trangThai);

    List<TranDau> findByGiaiDauIdAndLoaiVong(Integer giaiDauId, String loaiVong);

    long countByGiaiDauIdAndTrangThaiIn(Integer giaiDauId, List<String> trangThais);

    /** Danh sach tran cua cac san ma Staff duoc phan cong — giong SanDuocGiao() ben C#. */
    @Query("select t from TranDau t "
            + "left join fetch t.bangDau left join fetch t.doiNha left join fetch t.doiKhach "
            + "where t.giaiDau.sanBongId in :sanIds "
            + "and t.giaiDau.trangThai = 'Active' "
            + "and (:trangThai is null or t.trangThai = :trangThai) "
            + "order by t.ngayThiDau, t.trangThai")
    List<TranDau> findChoStaff(@Param("sanIds") List<Integer> sanIds, @Param("trangThai") String trangThai);

    @Query("select t from TranDau t "
            + "left join fetch t.bangDau left join fetch t.doiNha left join fetch t.doiKhach "
            + "where t.giaiDau.sanBongId in :sanIds "
            + "and t.giaiDau.trangThai = 'Active' and t.ngayThiDau >= :from and t.ngayThiDau < :to "
            + "and (:trangThai is null or t.trangThai = :trangThai) "
            + "order by t.ngayThiDau, t.trangThai")
    List<TranDau> findChoStaffTrongKhoang(@Param("sanIds") List<Integer> sanIds,
                                           @Param("from") LocalDateTime from,
                                           @Param("to") LocalDateTime to,
                                           @Param("trangThai") String trangThai);
}
