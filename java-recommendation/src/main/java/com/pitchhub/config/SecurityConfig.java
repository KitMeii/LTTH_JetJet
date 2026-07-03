package com.pitchhub.config;

import com.pitchhub.security.JwtFilter;
import org.springframework.context.annotation.Bean;
import org.springframework.context.annotation.Configuration;
import org.springframework.security.config.annotation.web.builders.HttpSecurity;
import org.springframework.security.config.annotation.web.configurers.AbstractHttpConfigurer;
import org.springframework.security.config.http.SessionCreationPolicy;
import org.springframework.security.web.SecurityFilterChain;
import org.springframework.security.web.authentication.UsernamePasswordAuthenticationFilter;

/**
 * REST API thuan — khong session, khong CSRF (giong tournament-service).
 * Cac controller o day (checkin/recommendation/report/tournament) da co san
 * "/api" trong @RequestMapping (khong dung server.servlet.context-path nhu
 * tournament-service) nen matcher phai khai day du "/api/...".
 * Tat ca endpoint hien co deu chi dung cho Owner.
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
                        .requestMatchers(
                                "/swagger-ui/**",
                                "/swagger-ui.html",
                                "/v3/api-docs/**",
                                "/v3/api-docs"
                        ).permitAll()
                        .requestMatchers("/api/owner/checkin/**").hasRole("Owner")
                        .requestMatchers("/api/owner/suggest-price").hasRole("Owner")
                        .requestMatchers("/api/report/pdf").hasRole("Owner")
                        .requestMatchers("/api/tournament/schedule").hasRole("Owner")
                        .requestMatchers("/api/tournament/validate").hasRole("Owner")
                        .anyRequest().authenticated()
                )
                .addFilterBefore(jwtFilter, UsernamePasswordAuthenticationFilter.class);

        return http.build();
    }
}
