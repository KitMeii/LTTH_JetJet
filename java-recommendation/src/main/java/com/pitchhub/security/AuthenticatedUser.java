package com.pitchhub.security;

/** Thong tin user rut ra tu JWT claims, gan vao SecurityContext moi request. */
public record AuthenticatedUser(Integer userId, String email, String role, String hoTen) {
}
