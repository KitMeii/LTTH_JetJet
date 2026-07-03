package com.pitchhub.security;

import io.jsonwebtoken.Claims;
import io.jsonwebtoken.JwtException;
import io.jsonwebtoken.Jwts;
import io.jsonwebtoken.security.Keys;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.stereotype.Component;

import javax.crypto.SecretKey;
import java.nio.charset.StandardCharsets;

/**
 * Doc/verify JWT do C# TokenHelper.TaoToken() phat hanh — cung logic voi
 * tournament-service/security/JwtUtil.java (chi khac package).
 * C# dung System.Security.Claims.ClaimTypes.* khi tao Claim truc tiep
 * tren JwtSecurityToken (khong qua ClaimsIdentity) nen claim type duoc
 * ghi nguyen dang URI dai, khong bi rut gon con "role"/"email".
 */
@Component
public class JwtUtil {

    public static final String CLAIM_USER_ID = "UserId";
    public static final String CLAIM_EMAIL = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress";
    public static final String CLAIM_ROLE = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role";
    public static final String CLAIM_NAME = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name";

    private final SecretKey signingKey;

    public JwtUtil(@Value("${jwt.secret}") String secret) {
        this.signingKey = Keys.hmacShaKeyFor(secret.getBytes(StandardCharsets.UTF_8));
    }

    public boolean validateToken(String token) {
        try {
            parseClaims(token);
            return true;
        } catch (JwtException | IllegalArgumentException ex) {
            return false;
        }
    }

    public Integer extractUserId(String token) {
        String raw = parseClaims(token).get(CLAIM_USER_ID, String.class);
        return raw != null ? Integer.parseInt(raw) : null;
    }

    public String extractRole(String token) {
        return parseClaims(token).get(CLAIM_ROLE, String.class);
    }

    public String extractEmail(String token) {
        return parseClaims(token).get(CLAIM_EMAIL, String.class);
    }

    public String extractName(String token) {
        return parseClaims(token).get(CLAIM_NAME, String.class);
    }

    private Claims parseClaims(String token) {
        return Jwts.parserBuilder()
                .setSigningKey(signingKey)
                .setAllowedClockSkewSeconds(0)
                .build()
                .parseClaimsJws(token)
                .getBody();
    }
}
