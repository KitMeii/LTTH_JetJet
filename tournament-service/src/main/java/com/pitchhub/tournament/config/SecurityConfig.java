package com.pitchhub.tournament.config;

import com.pitchhub.tournament.security.JwtFilter;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.annotation.web.configurers.AbstractHttpConfigurer;
import org.springframework.security.config.http.SessionCreationPolicy;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.security.web.authentication.UsernamePasswordAuthenticationFilter;

/**
 * REST API thuan — khong session, khong CSRF (giong cach C# dung
 * JWT nhung o day doc qua Authorization header thay vi cookie session MVC).
 * Phan quyen theo role tuong ung [YeuCauDangNhap("...")] ben C#:
 *   Admin, Owner, Staff, User (khong co role "Captain" rieng — doi truong
 *   chi la User/Owner/Staff/Admin binh thuong dung DoiTruongId).
 */
@Configuration
public class SecurityConfig {

    private final JwtFilter jwtFilter;

    public SecurityConfig(JwtFilter jwtFilter) {
        this.jwtFilter = jwtFilter;
    }

    @Bean
    public SecurityFilterChain securityFilterChain(HttpSecurity http) throws Exception {
        http
                .csrf(AbstractHttpConfigurer::disable)
                .sessionManagement(sm -> sm.sessionCreationPolicy(SessionCreationPolicy.STATELESS))
                .authorizeHttpRequests(auth -> auth
                        .requestMatchers("/public/**").permitAll()
                        .requestMatchers("/actuator/**").permitAll()
                        .requestMatchers("/swagger-ui/**", "/swagger-ui.html", "/v3/api-docs/**", "/v3/api-docs").permitAll()
                        .requestMatchers("/admin/**").hasRole("Admin")
                        .requestMatchers("/tournament/owner/**").hasRole("Owner")
                        .requestMatchers("/tournament/staff/**").hasRole("Staff")
                        .anyRequest().authenticated()
                )
                .addFilterBefore(jwtFilter, UsernamePasswordAuthenticationFilter.class);

        return http.build();
    }
}
