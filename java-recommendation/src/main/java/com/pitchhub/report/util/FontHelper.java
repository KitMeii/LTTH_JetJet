package com.pitchhub.report.util;

import com.lowagie.text.Font;
import com.lowagie.text.pdf.BaseFont;

import java.io.File;
import java.io.IOException;
import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Path;

public final class FontHelper {

    private static BaseFont CACHED;

    private FontHelper() {}

    public static synchronized BaseFont getUnicodeBaseFont() {
        if (CACHED != null) return CACHED;

        String[] candidates = {
                "C:/Windows/Fonts/arial.ttf",
                "C:/Windows/Fonts/ARIAL.TTF",
                "C:/Windows/Fonts/tahoma.ttf",
                "C:/Windows/Fonts/times.ttf",
                "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf",
                "/Library/Fonts/Arial.ttf"
        };
        for (String p : candidates) {
            File f = new File(p);
            if (f.exists()) {
                try {
                    CACHED = BaseFont.createFont(p, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                    return CACHED;
                } catch (Exception ignore) {}
            }
        }

        try (InputStream in = FontHelper.class.getResourceAsStream("/fonts/DejaVuSans.ttf")) {
            if (in != null) {
                Path tmp = Files.createTempFile("pdf-font-", ".ttf");
                Files.copy(in, tmp, java.nio.file.StandardCopyOption.REPLACE_EXISTING);
                CACHED = BaseFont.createFont(tmp.toAbsolutePath().toString(),
                        BaseFont.IDENTITY_H, BaseFont.EMBEDDED);
                return CACHED;
            }
        } catch (IOException ignore) {}

        try {
            CACHED = BaseFont.createFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED);
            return CACHED;
        } catch (Exception e) {
            throw new IllegalStateException("Không tìm thấy font Unicode cho PDF", e);
        }
    }

    public static Font font(float size, int style) {
        return new Font(getUnicodeBaseFont(), size, style);
    }
}
