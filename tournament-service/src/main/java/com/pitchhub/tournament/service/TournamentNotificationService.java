package com.pitchhub.tournament.service;

import com.pitchhub.tournament.model.DoiBong;
import com.pitchhub.tournament.model.GiaiDau;
import com.pitchhub.tournament.model.TranDau;
import com.pitchhub.tournament.model.User;
import com.pitchhub.tournament.repository.DoiBongRepository;
import com.pitchhub.tournament.repository.GiaiDauRepository;
import com.pitchhub.tournament.repository.TranDauRepository;
import com.pitchhub.tournament.repository.UserRepository;
import jakarta.mail.internet.MimeMessage;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.beans.factory.annotation.Value;
import org.springframework.mail.javamail.JavaMailSender;
import org.springframework.mail.javamail.MimeMessageHelper;
import org.springframework.stereotype.Service;

import java.util.Collection;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

/**
 * Toan bo email/notification cho giai dau — tach khoi nghiep vu,
 * fail khong anh huong domain (bat exception, chi log).
 * Port tu TournamentNotificationService.cs + hoan thien guiEmailXacNhanThanhToan
 * (truoc day chi la comment "PATCH", chua bao gio active).
 */
@Service
public class TournamentNotificationService {

    private static final Logger log = LoggerFactory.getLogger(TournamentNotificationService.class);

    private final GiaiDauRepository giaiDauRepository;
    private final TranDauRepository tranDauRepository;
    private final DoiBongRepository doiBongRepository;
    private final UserRepository userRepository;
    private final JavaMailSender mailSender;

    @Value("${spring.mail.username}")
    private String fromAddress;

    @Value("${mail.sender-name}")
    private String senderName;

    public TournamentNotificationService(GiaiDauRepository giaiDauRepository,
                                          TranDauRepository tranDauRepository,
                                          DoiBongRepository doiBongRepository,
                                          UserRepository userRepository,
                                          JavaMailSender mailSender) {
        this.giaiDauRepository = giaiDauRepository;
        this.tranDauRepository = tranDauRepository;
        this.doiBongRepository = doiBongRepository;
        this.userRepository = userRepository;
        this.mailSender = mailSender;
    }

    // ══════════════════════════════════════════════════════════
    // Gui email lich thi dau cho doi truong khi giai khoi dong
    // ══════════════════════════════════════════════════════════
    public void guiEmailLichDau(Integer giaiDauId) {
        GiaiDau giai = giaiDauRepository.findDetailById(giaiDauId).orElse(null);
        if (giai == null) return;

        String lichHtml = buildLichHtml(giai.getTranDaus());
        String linkGiai = "https://pitchhub.vn/TournamentPublic/Details/" + giaiDauId;

        Set<Integer> daGui = new LinkedHashSet<>();
        for (DoiBong doi : giai.getDoiBongs()) {
            if (!Boolean.TRUE.equals(doi.getDaThanhToan()) || doi.getDoiTruongId() == null) continue;
            if (!daGui.add(doi.getDoiTruongId())) continue;

            User dt = userRepository.findById(doi.getDoiTruongId()).orElse(null);
            if (dt == null) continue;

            String body = """
                <div style='font-family:Arial,sans-serif;max-width:600px;margin:0 auto;'>
                    <div style='background:linear-gradient(135deg,#0f2027,#0EA86A);
                                padding:24px;text-align:center;border-radius:12px 12px 0 0;'>
                        <div style='font-size:1.5rem;font-weight:900;color:#fff;'>
                            PITCH<span style='color:#1ed760;'>HUB</span> &#9917;
                        </div>
                    </div>
                    <div style='padding:24px;background:#fff;border-radius:0 0 12px 12px;'>
                        <h2>&#127942; %s &mdash; Lich thi dau</h2>
                        <p>Xin chao <strong>%s</strong>, giai dau da bat dau!</p>
                        %s
                        <div style='margin-top:20px;text-align:center;'>
                            <a href='%s' style='background:#0EA86A;color:#fff;
                               padding:12px 24px;border-radius:10px;
                               text-decoration:none;font-weight:700;'>
                                Xem truc tiep BXH &amp; Lich dau &rarr;
                            </a>
                        </div>
                    </div>
                </div>"""
                    .formatted(giai.getTenGiai(), dt.getHoTen(), lichHtml, linkGiai);

            guiAn(dt.getEmail(), dt.getHoTen(), "Lich thi dau — " + giai.getTenGiai(), body);
        }
    }

    // ══════════════════════════════════════════════════════════
    // HOAN THIEN: Gui email xac nhan thanh toan sau khi Owner xac nhan
    // (truoc day chi la comment "PATCH", chua bao gio duoc goi)
    // ══════════════════════════════════════════════════════════
    public void guiEmailXacNhanThanhToan(Integer doiId) {
        // Goi tu TournamentService sau khi da luu DaThanhToan=true — tra cuu lai
        // qua repository de khong phai truyen ca object doi qua nhieu tang.
        var doi = doiBongOrNull(doiId);
        if (doi == null || doi.getDoiTruongId() == null) return;

        GiaiDau giai = giaiDauRepository.findById(doi.getGiaiDauId()).orElse(null);
        User dt = userRepository.findById(doi.getDoiTruongId()).orElse(null);
        if (giai == null || dt == null) return;

        String linkGiai = "https://pitchhub.vn/TournamentPublic/Details/" + doi.getGiaiDauId();
        String body = """
            <div style='font-family:Arial,sans-serif;max-width:560px;margin:0 auto;'>
                <div style='background:linear-gradient(135deg,#0f2027,#0EA86A);
                            padding:24px;text-align:center;border-radius:12px 12px 0 0;'>
                    <div style='font-size:1.5rem;font-weight:900;color:#fff;'>
                        PITCH<span style='color:#1ed760;'>HUB</span> &#9917;
                    </div>
                </div>
                <div style='padding:24px;background:#fff;border-radius:0 0 12px 12px;'>
                    <h2>&#9989; Da xac nhan thanh toan!</h2>
                    <p>Xin chao <strong>%s</strong>,</p>
                    <p>Doi <strong style='color:#0EA86A;'>%s</strong> da duoc xac nhan
                       thanh toan le phi + ky quy cho giai
                       <strong>%s</strong>.</p>
                    <div style='background:#f0f9f5;border-radius:10px;padding:16px;margin:16px 0;'>
                        <div style='font-size:13px;color:#666;margin-bottom:4px;'>Buoc tiep theo</div>
                        <div style='font-weight:700;color:#0EA86A;'>
                            Nop danh sach cau thu truoc khi giai dong dang ky!
                        </div>
                    </div>
                    <a href='%s' style='display:inline-block;background:#0EA86A;
                       color:#fff;padding:12px 24px;border-radius:10px;
                       text-decoration:none;font-weight:700;'>
                        Xem giai dau &rarr;
                    </a>
                </div>
            </div>""".formatted(dt.getHoTen(), doi.getTenDoi(), giai.getTenGiai(), linkGiai);

        guiAn(dt.getEmail(), dt.getHoTen(), "Xac nhan thanh toan — " + doi.getTenDoi(), body);
    }

    // ══════════════════════════════════════════════════════════
    // Gui email xac nhan dang ky doi (ngay sau khi tao doi, truoc khi thanh toan)
    // ══════════════════════════════════════════════════════════
    public void guiEmailXacNhanDangKy(Integer doiId) {
        var doi = doiBongOrNull(doiId);
        if (doi == null || doi.getDoiTruongId() == null) return;

        GiaiDau giai = giaiDauRepository.findById(doi.getGiaiDauId()).orElse(null);
        User dt = userRepository.findById(doi.getDoiTruongId()).orElse(null);
        if (giai == null || dt == null) return;

        String linkGiai = "https://pitchhub.vn/TournamentPublic/Details/" + doi.getGiaiDauId();
        String body = """
            <div style='font-family:Arial,sans-serif;padding:20px;'>
                <h2>Dang ky thanh cong!</h2>
                <p>Xin chao <strong>%s</strong>, doi <strong>%s</strong> da dang ky
                   tham du giai <strong>%s</strong>. Vui long cho Owner xac nhan
                   sau khi ban chuyen khoan le phi + ky quy.</p>
                <a href='%s'>Xem giai dau</a>
            </div>""".formatted(dt.getHoTen(), doi.getTenDoi(), giai.getTenGiai(), linkGiai);

        guiAn(dt.getEmail(), dt.getHoTen(), "Dang ky giai '" + giai.getTenGiai() + "' thanh cong!", body);
    }

    // ══════════════════════════════════════════════════════════
    // Gui email ket qua tran cho 2 doi truong
    // ══════════════════════════════════════════════════════════
    public void guiEmailKetQuaTran(Integer tranDauId) {
        TranDau tran = tranDauRepository.findById(tranDauId).orElse(null);
        if (tran == null) return;

        GiaiDau giai = giaiDauRepository.findById(tran.getGiaiDauId()).orElse(null);
        String tyso = tran.getBanThangNha() + " - " + tran.getBanThangKhach();
        String tieuDe = "Ket qua: " + tenDoiAn(tran.getDoiNha()) + " " + tyso + " " + tenDoiAn(tran.getDoiKhach());

        for (Integer doiTruongId : doiTruongIds(tran)) {
            User dt = userRepository.findById(doiTruongId).orElse(null);
            if (dt == null) continue;

            String body = """
                <div style='font-family:Arial;padding:20px;'>
                    <h2>Ket qua tran dau</h2>
                    <div style='font-size:2rem;font-weight:900;text-align:center;
                                color:#0EA86A;padding:20px;'>%s</div>
                    <p>%s vs %s</p>
                    <p>Vong %s &mdash; %s</p>
                </div>""".formatted(tyso, tenDoiAn(tran.getDoiNha()), tenDoiAn(tran.getDoiKhach()),
                    tran.getVongDau(), giai != null ? giai.getTenGiai() : "");

            guiAn(dt.getEmail(), dt.getHoTen(), tieuDe, body);
        }
    }

    private List<Integer> doiTruongIds(TranDau tran) {
        List<Integer> ids = new java.util.ArrayList<>();
        if (tran.getDoiNha() != null && tran.getDoiNha().getDoiTruongId() != null) ids.add(tran.getDoiNha().getDoiTruongId());
        if (tran.getDoiKhach() != null && tran.getDoiKhach().getDoiTruongId() != null) ids.add(tran.getDoiKhach().getDoiTruongId());
        return ids;
    }

    private String tenDoiAn(DoiBong doi) {
        return doi != null ? doi.getTenDoi() : "?";
    }

    private DoiBong doiBongOrNull(Integer doiId) {
        return doiId != null ? doiBongRepository.findById(doiId).orElse(null) : null;
    }

    // ── Helper build HTML lich dau ─────────────────────────
    private String buildLichHtml(Collection<TranDau> tranDaus) {
        StringBuilder html = new StringBuilder("""
                <table style='border-collapse:collapse;width:100%;'>
                <tr style='background:#0EA86A;color:#fff;'>
                    <th style='padding:8px;border:1px solid #ddd;'>Vong</th>
                    <th style='padding:8px;border:1px solid #ddd;'>Doi nha</th>
                    <th style='padding:8px;border:1px solid #ddd;'>Doi khach</th>
                    <th style='padding:8px;border:1px solid #ddd;'>Ngay</th>
                </tr>""");

        tranDaus.stream().sorted((a, b) -> Integer.compare(a.getVongDau(), b.getVongDau()))
                .forEach(t -> html.append("""
                    <tr>
                        <td style='padding:8px;border:1px solid #ddd;text-align:center;'>Vong %s</td>
                        <td style='padding:8px;border:1px solid #ddd;'>%s</td>
                        <td style='padding:8px;border:1px solid #ddd;'>%s</td>
                        <td style='padding:8px;border:1px solid #ddd;'>%s</td>
                    </tr>""".formatted(t.getVongDau(), tenDoiAn(t.getDoiNha()), tenDoiAn(t.getDoiKhach()),
                        t.getNgayThiDau().toLocalDate())));

        html.append("</table>");
        return html.toString();
    }

    private void guiAn(String toEmail, String toName, String subject, String htmlBody) {
        try {
            MimeMessage message = mailSender.createMimeMessage();
            MimeMessageHelper helper = new MimeMessageHelper(message, true, "UTF-8");
            helper.setFrom(fromAddress, senderName);
            helper.setTo(toEmail);
            helper.setSubject(subject);
            helper.setText(htmlBody, true);
            mailSender.send(message);
        } catch (Exception ex) {
            // fire-and-forget — loi gui mail khong duoc lam rollback nghiep vu chinh
            log.warn("Gui email that bai toi {}: {}", toEmail, ex.getMessage());
        }
    }
}
