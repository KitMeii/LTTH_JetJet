package com.pitchhub.tournament.model;

import jakarta.persistence.*;

import java.util.ArrayList;
import java.util.List;

/** Map bang BangDaus (bang dau: Bang A, Bang B, ...). */
@Entity
@Table(name = "BangDaus")
public class BangDau {

    @Id
    @GeneratedValue(strategy = GenerationType.IDENTITY)
    private Integer id;

    @Column(name = "GiaiDauId", nullable = false, insertable = false, updatable = false)
    private Integer giaiDauId;

    @Column(name = "TenBang", nullable = false)
    private String tenBang;

    @ManyToOne(fetch = FetchType.LAZY)
    @JoinColumn(name = "GiaiDauId", nullable = false)
    private GiaiDau giaiDau;

    @OneToMany(mappedBy = "bang")
    private List<DoiBong> doiBongs = new ArrayList<>();

    @OneToMany(mappedBy = "bangDau")
    private List<TranDau> tranDaus = new ArrayList<>();

    public BangDau() {}

    public Integer getId() { return id; }
    public void setId(Integer id) { this.id = id; }

    public Integer getGiaiDauId() { return giaiDauId; }
    public void setGiaiDauId(Integer giaiDauId) { this.giaiDauId = giaiDauId; }

    public String getTenBang() { return tenBang; }
    public void setTenBang(String tenBang) { this.tenBang = tenBang; }

    public GiaiDau getGiaiDau() { return giaiDau; }
    public void setGiaiDau(GiaiDau giaiDau) { this.giaiDau = giaiDau; }

    public List<DoiBong> getDoiBongs() { return doiBongs; }
    public void setDoiBongs(List<DoiBong> doiBongs) { this.doiBongs = doiBongs; }

    public List<TranDau> getTranDaus() { return tranDaus; }
    public void setTranDaus(List<TranDau> tranDaus) { this.tranDaus = tranDaus; }
}
