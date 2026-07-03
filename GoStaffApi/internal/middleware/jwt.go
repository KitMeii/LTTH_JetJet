package middleware

import (
	"crypto/hmac"
	"crypto/sha256"
	"crypto/subtle"
	"encoding/base64"
	"encoding/json"
	"net/http"
	"strconv"
	"strings"
	"time"

	"github.com/gin-gonic/gin"
)

// JWT HS256 validation viet thu cong bang thu vien chuan (khong them
// dependency moi vao go.mod) — vi may nay chua cai Go nen khong the chay
// `go get`/sua lai go.sum cho dung hash. Cung thuat toan + cung claim key
// voi tournament-service/security/JwtUtil.java (JJWT ben Java) va
// TokenHelper.cs (System.IdentityModel.Tokens.Jwt ben C#).
//
// C# TokenHelper.TaoToken() dung System.Security.Claims.ClaimTypes.* khi tao
// Claim truc tiep tren JwtSecurityToken (khong qua ClaimsIdentity) nen ten
// claim duoc ghi nguyen dang URI dai — PHAI khop chinh xac 2 hang duoi day
// voi JwtUtil.java, khong duoc rut gon con "role"/"userId".
const (
	claimUserID = "UserId"
	claimRole   = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
)

// Context key dung chung — handler doc qua c.GetInt("staffId") / c.GetString("role").
const (
	ContextStaffID = "staffId"
	ContextRole    = "role"
)

func JWTMiddleware(secret string) gin.HandlerFunc {
	key := []byte(secret)

	return func(c *gin.Context) {
		token, ok := extractToken(c)
		if !ok {
			c.AbortWithStatusJSON(http.StatusUnauthorized, gin.H{"ok": false, "message": "missing jwt token"})
			return
		}

		claims, err := parseAndVerify(token, key)
		if err != nil {
			c.AbortWithStatusJSON(http.StatusUnauthorized, gin.H{"ok": false, "message": "invalid or expired jwt: " + err.Error()})
			return
		}

		userIDRaw, _ := claims[claimUserID].(string)
		staffID, err := strconv.Atoi(userIDRaw)
		if err != nil || staffID <= 0 {
			c.AbortWithStatusJSON(http.StatusUnauthorized, gin.H{"ok": false, "message": "jwt missing valid UserId claim"})
			return
		}

		role, _ := claims[claimRole].(string)
		if role != "Staff" {
			c.AbortWithStatusJSON(http.StatusForbidden, gin.H{"ok": false, "message": "role Staff required"})
			return
		}

		c.Set(ContextStaffID, staffID)
		c.Set(ContextRole, role)
		c.Next()
	}
}

// extractToken: doc cookie "jwt" truoc (frontend C# dang luu token trong
// cookie), fallback sang header "Authorization: Bearer xxx" — cung thu tu
// uu tien voi JwtFilter.java (tournament-service) va RecommendationApiService
// (java-recommendation).
func extractToken(c *gin.Context) (string, bool) {
	if cookieToken, err := c.Cookie("jwt"); err == nil && cookieToken != "" {
		return cookieToken, true
	}

	auth := c.GetHeader("Authorization")
	if strings.HasPrefix(auth, "Bearer ") {
		return strings.TrimPrefix(auth, "Bearer "), true
	}

	return "", false
}

func parseAndVerify(token string, key []byte) (map[string]interface{}, error) {
	parts := strings.Split(token, ".")
	if len(parts) != 3 {
		return nil, errInvalidToken
	}

	signingInput := parts[0] + "." + parts[1]
	expectedSig := signFn(signingInput, key)

	actualSig, err := base64.RawURLEncoding.DecodeString(parts[2])
	if err != nil {
		return nil, errInvalidToken
	}

	if subtle.ConstantTimeCompare(expectedSig, actualSig) != 1 {
		return nil, errInvalidSignature
	}

	payloadJSON, err := base64.RawURLEncoding.DecodeString(parts[1])
	if err != nil {
		return nil, errInvalidToken
	}

	var claims map[string]interface{}
	if err := json.Unmarshal(payloadJSON, &claims); err != nil {
		return nil, errInvalidToken
	}

	if exp, ok := claims["exp"]; ok {
		expUnix, ok := toInt64(exp)
		if ok && time.Now().Unix() > expUnix {
			return nil, errTokenExpired
		}
	}

	return claims, nil
}

func signFn(input string, key []byte) []byte {
	mac := hmac.New(sha256.New, key)
	mac.Write([]byte(input))
	return mac.Sum(nil)
}

func toInt64(v interface{}) (int64, bool) {
	switch n := v.(type) {
	case float64:
		return int64(n), true
	case int64:
		return n, true
	case json.Number:
		i, err := n.Int64()
		return i, err == nil
	default:
		return 0, false
	}
}

type jwtError string

func (e jwtError) Error() string { return string(e) }

const (
	errInvalidToken     = jwtError("malformed token")
	errInvalidSignature = jwtError("signature mismatch")
	errTokenExpired     = jwtError("token expired")
)
