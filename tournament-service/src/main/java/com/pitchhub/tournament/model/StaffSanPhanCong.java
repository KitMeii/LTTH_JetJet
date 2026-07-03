package com.pitchhub.tournament.model;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.Setter;
import lombok.NoArgsConstructor;

/**
 * Entity tham chieu CHI DOC toi bang StaffSanPhanCong (thuoc domain chinh cua C#).
 * Dung de xac dinh Staff duoc phan cong san nao — TournamentStaffController
 * loc danh sach tran dau theo cac SanBongId nay (giong SanDuocGiao() ben C#).
 */
@Entity
@Table(name = "StaffSanPhanCong")
@Getter
@Setter
@NoArgsConstructor
public class StaffSanPhanCong {

    @Id
    private Integer id;

    @Column(name = "StaffId", nullable = false)
    private Integer staffId;

    @Column(name = "SanBongId", nullable = false)
    private Integer sanBongId;
}
