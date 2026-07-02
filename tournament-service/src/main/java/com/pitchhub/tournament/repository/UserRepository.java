package com.pitchhub.tournament.repository;

import com.pitchhub.tournament.model.User;
import org.springframework.data.jpa.repository.JpaRepository;

/** Chi doc — xem ghi chu trong model/User.java. */
public interface UserRepository extends JpaRepository<User, Integer> {
}
