// tournament-realtime.js — PitchHub
// SignalR client cho trang public Details giải đấu
// Nhúng vào TournamentPublic/Details.cshtml trong @section Scripts

(function () {
    'use strict';

    // Đọc giaiId từ data attribute của body hoặc script tag
    var giaiId = window.__giaiDauId;
    if (!giaiId) return;

    // ── Khởi tạo connection ──────────────────────────────────────
    var conn = new signalR.HubConnectionBuilder()
        .withUrl('/tournamentHub')
        .withAutomaticReconnect([1000, 2000, 5000, 10000])
        .build();

    // ── Xử lý sự kiện từ server ──────────────────────────────────

    // BXH mới → render lại bảng
    conn.on('CapNhatBXH', function (bxhData) {
        renderBXH(bxhData);
        showToast('📊 Bảng xếp hạng vừa cập nhật!', 'info');
    });

    // Tỷ số trực tiếp → cập nhật card trận
    conn.on('CapNhatTyso', function (tyso) {
        var el = document.getElementById('tyso-' + tyso.tranDauId);
        if (el) {
            el.textContent = tyso.tysoNha + ' – ' + tyso.tysoKhach;
            el.style.color = 'var(--green)';
            // Hiệu ứng pulse
            el.classList.add('score-updated');
            setTimeout(function () { el.classList.remove('score-updated'); }, 800);
        }
    });

    // Sự kiện mới (bàn thắng, thẻ)
    conn.on('SuKienMoi', function (sk) {
        var ico = sk.loaiSuKien === 'BanThang' ? '⚽' :
            sk.loaiSuKien === 'TheVang' ? '💛' :
                sk.loaiSuKien === 'TheDo' ? '🟥' :
                    sk.loaiSuKien === 'TheVangLan2' ? '🟥' : '📋';

        showToast(
            ico + ' ' + (sk.tenDoi || '') + ' — ' + sk.loaiSuKien +
            (sk.phut ? ' (' + sk.phut + "\')" : ''),
            sk.loaiSuKien === 'BanThang' ? 'success' : 'warning'
        );
    });

    // Trận kết thúc
    conn.on('TranKetThuc', function (data) {
        var el = document.getElementById('state-' + data.tranDauId);
        if (el) {
            el.textContent = '✅ Kết thúc';
            el.style.background = 'rgba(15,23,42,.1)';
            el.style.color = 'var(--muted)';
        }
        showToast('🏁 Một trận vừa kết thúc!', 'info');
    });

    // ── Kết nối ──────────────────────────────────────────────────
    conn.start()
        .then(function () {
            conn.invoke('ThamGiaGiai', String(giaiId));
            updateConnectionStatus(true);
        })
        .catch(function (err) {
            console.warn('[TournamentHub] connect error:', err);
            updateConnectionStatus(false);
        });

    conn.onreconnected(function () {
        conn.invoke('ThamGiaGiai', String(giaiId));
        updateConnectionStatus(true);
        showToast('🔄 Đã kết nối lại!', 'success');
    });

    conn.onclose(function () {
        updateConnectionStatus(false);
    });

    // ── Render BXH ───────────────────────────────────────────────
    function renderBXH(bxhData) {
        // bxhData = { bangId: [StandingRow, ...], ... }
        Object.keys(bxhData).forEach(function (bangId) {
            var rows = bxhData[bangId];
            var tbody = document.getElementById('bxh-tbody-' + bangId);
            if (!tbody) return;

            tbody.innerHTML = '';
            rows.forEach(function (r, idx) {
                var rankClass = idx === 0 ? 'color:#FFD700;font-weight:800' :
                    idx === 1 ? 'color:#C0C0C0;font-weight:700' :
                        idx === 2 ? 'color:#CD7F32;font-weight:700' : '';
                var hieuSoColor = r.hieuSo > 0 ? 'color:var(--green)' :
                    r.hieuSo < 0 ? 'color:var(--danger)' : '';
                var hieuSoStr = (r.hieuSo > 0 ? '+' : '') + r.hieuSo;

                var tr = document.createElement('tr');
                tr.innerHTML =
                    '<td style="' + rankClass + '">' + (idx + 1) + '</td>' +
                    '<td style="font-weight:600">' + r.tenDoi + '</td>' +
                    '<td>' + r.soTran + '</td>' +
                    '<td style="color:var(--green)">' + r.thang + '</td>' +
                    '<td>' + r.hoa + '</td>' +
                    '<td style="color:var(--danger)">' + r.thua + '</td>' +
                    '<td>' + r.banThang + '</td>' +
                    '<td>' + r.banThua + '</td>' +
                    '<td style="' + hieuSoColor + '">' + hieuSoStr + '</td>' +
                    '<td><strong style="font-family:var(--ff-h);font-size:15px;color:var(--green)">'
                    + r.diem + '</strong></td>';
                tbody.appendChild(tr);
            });
        });
    }

    // ── Trạng thái kết nối ───────────────────────────────────────
    function updateConnectionStatus(connected) {
        var dot = document.getElementById('realtimeDot');
        var lbl = document.getElementById('realtimeLbl');
        if (dot) dot.style.background = connected ? 'var(--green)' : '#ef4444';
        if (lbl) lbl.textContent = connected ? 'LIVE' : 'OFFLINE';
    }

    // ── Toast notification ───────────────────────────────────────
    var toastQueue = [];
    function showToast(msg, type) {
        var container = document.getElementById('toastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'toastContainer';
            container.style.cssText =
                'position:fixed;bottom:24px;right:24px;z-index:9999;' +
                'display:flex;flex-direction:column;gap:8px;';
            document.body.appendChild(container);
        }

        var colors = {
            success: { bg: 'rgba(14,168,106,.95)', icon: '✅' },
            warning: { bg: 'rgba(245,158,11,.95)', icon: '⚠️' },
            info: { bg: 'rgba(14,165,233,.95)', icon: 'ℹ️' },
        };
        var c = colors[type] || colors.info;

        var toast = document.createElement('div');
        toast.style.cssText =
            'background:' + c.bg + ';color:#fff;padding:12px 18px;' +
            'border-radius:10px;font-size:13px;font-weight:600;' +
            'box-shadow:0 4px 20px rgba(0,0,0,.3);' +
            'animation:slideInToast .25s ease;max-width:320px;';
        toast.textContent = c.icon + ' ' + msg;
        container.appendChild(toast);

        setTimeout(function () {
            toast.style.opacity = '0';
            toast.style.transition = 'opacity .3s';
            setTimeout(function () { toast.remove(); }, 300);
        }, 3500);
    }

    // Inject animation
    if (!document.getElementById('toastStyle')) {
        var style = document.createElement('style');
        style.id = 'toastStyle';
        style.textContent =
            '@keyframes slideInToast { from{opacity:0;transform:translateX(20px)} to{opacity:1;transform:translateX(0)} }' +
            '.score-updated { animation: scorePulse .4s ease; }' +
            '@keyframes scorePulse { 0%{transform:scale(1)} 50%{transform:scale(1.3);color:#1ed760} 100%{transform:scale(1)} }';
        document.head.appendChild(style);
    }
})();