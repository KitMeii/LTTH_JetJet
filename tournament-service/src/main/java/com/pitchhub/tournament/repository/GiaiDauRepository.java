package com.pitchhub.tournament.repository;

import com.pitchhub.tournament.model.GiaiDau;
import org.springframework.data.jpa.repository.EntityGraph;
import org.springframework.data.jpa.repository.JpaRepository;
import org.springframework.data.jpa.repository.Query;

import java.util.List;
import java.util.Optional;

public interface GiaiDauRepository extends JpaRepository<GiaiDau, Integer> {

    List<GiaiDau> findByTrangThai(String trangThai);

    List<GiaiDau> findByOwnerId(Integer ownerId);

    List<GiaiDau> findByTrangThaiIn(List<String> statuses);

    @EntityGraph(attributePaths = {"bangDaus", "doiBongs", "doiBongs.thanhViens",
            "doiBongs.bang", "tranDaus", "tranDaus.bangDau", "tranDaus.doiNha", "tranDaus.doiKhach", "tranDaus.suKiens"})
    @Query("select g from GiaiDau g where g.id = :id")
    Optional<GiaiDau> findDetailById(Integer id);
}
