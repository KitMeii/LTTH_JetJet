package com.pitchhub.tournament.model;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.Setter;
import lombok.NoArgsConstructor;

/**
 * Entity tham chieu CHI DOC toi bang SanBongs (thuoc domain chinh cua C#).
 * Tournament-service khong bao gio insert/update/delete bang nay —
 * chi can de join lay TenSan/Quan/OwnerId khi tao/hien thi giai dau.
 */
@Entity
@Table(name = "SanBongs")
@Getter
@Setter
@NoArgsConstructor
public class SanBong {

    @Id
    private Integer id;

    @Column(name = "TenSan", nullable = false)
    private String tenSan;

    @Column(name = "Quan", nullable = false)
    private String quan;

    @Column(name = "OwnerId", nullable = false)
    private Integer ownerId;

    @Column(name = "TrangThaiDuyet", nullable = false)
    private String trangThaiDuyet;

    @Column(name = "IsHidden", nullable = false)
    private Boolean isHidden;
}
