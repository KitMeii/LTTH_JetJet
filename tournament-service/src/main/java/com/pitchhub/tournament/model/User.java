package com.pitchhub.tournament.model;

import jakarta.persistence.*;
import lombok.Getter;
import lombok.Setter;
import lombok.NoArgsConstructor;

/**
 * Entity tham chieu CHI DOC toi bang Users (thuoc domain chinh cua C#).
 * Tournament-service khong bao gio insert/update/delete bang nay —
 * chi can de join lay HoTen/Email hien thi (Owner, DoiTruong cua doi bong).
 */
@Entity
@Table(name = "Users")
@Getter
@Setter
@NoArgsConstructor
public class User {

    @Id
    private Integer id;

    @Column(name = "HoTen", nullable = false)
    private String hoTen;

    @Column(name = "Email", nullable = false)
    private String email;

    @Column(name = "VaiTro", nullable = false)
    private String vaiTro;

    @Column(name = "IsActive", nullable = false)
    private Boolean isActive;
}
