// venues-details.js v4.0 — PitchHub
// ── Helper ────────────────────────────────────────────────────
function $(id) { return document.getElementById(id); }
function _fmt(n) { return Number(n).toLocaleString('vi-VN') + '\u0111'; }

// ── State ─────────────────────────────────────────────────────
var _selectedKhungGioId = null;
var _selectedGia = 0;
var _tyLeCoc = 0.3;
var _dichVuMap = {};   // { id: gia }
var _dichVuSL = {};   // { id: soLuong }
var _tongDV = 0;       // tổng tiền dịch vụ hiện tại (dùng bởi apDungVoucher)

// ── Đăng ký dịch vụ (gọi từ Razor trước initVenuesDetails) ───
function registerDichVu(id, gia) {
    id = String(id);
    _dichVuMap[id] = parseFloat(gia) || 0;
    _dichVuSL[id] = 0;
}

// ── Khởi tạo ─────────────────────────────────────────────────
function initVenuesDetails(sanId, tenSan) {
    window._sanId = sanId;
    window._tenSan = tenSan;

    var tyLeEl = $('tyLeCocInput');
    if (tyLeEl) _tyLeCoc = parseFloat(tyLeEl.value) || 0.3;

    _setPanel(false);

    // Sync date input → hidden field
    var dateInput = $('ngayThiDau');
    var ngayInput = $('ngayInput');
    if (dateInput && ngayInput) {
        ngayInput.value = dateInput.value;
        dateInput.addEventListener('change', function () {
            ngayInput.value = this.value;
        });
    }

    // SignalR
    try {
        var conn = new signalR.HubConnectionBuilder()
            .withUrl('/sanBongHub')
            .withAutomaticReconnect()
            .build();
        conn.on('CapNhatTrangThaiSlot', function (sanBongId, slots) {
            if (parseInt(sanBongId) === parseInt(sanId)) _capNhatLuoi(slots);
        });
        conn.on('CapNhatKhungGio', function (data) { _capNhatSlot(data); });
        conn.start().catch(function (e) { console.warn('SignalR:', e); });
    } catch (e) { console.warn('SignalR kh\u00f4ng kh\u1ea3 d\u1ee5ng'); }
}

// ── Chọn khung giờ ───────────────────────────────────────────
function chonKhungGio(el) {
    if (el.dataset.status === 'DaDat') return;
    if (el.classList.contains('daqua')) return;
    if (el.dataset.status === 'DangGiu') {
        alert('Khung gi\u1edd n\u00e0y \u0111ang \u0111\u01b0\u1ee3c gi\u1eef ch\u1ed7. Vui l\u00f2ng ch\u1ecdn khung gi\u1edd kh\u00e1c!');
        return;
    }

    _selectedKhungGioId = parseInt(el.dataset.id);
    _selectedGia = parseFloat(el.dataset.gia) || 0;

    var tyLeEl = $('tyLeCocInput');
    if (tyLeEl) _tyLeCoc = parseFloat(tyLeEl.value) || 0.3;

    document.querySelectorAll('.gio-item').forEach(function (c) {
        c.classList.remove('selected');
    });
    el.classList.add('selected');

    var slotTimeEl = $('slotTime');
    if (slotTimeEl) slotTimeEl.textContent = el.dataset.batdau + ' \u2013 ' + el.dataset.ketthuc;

    var kgInput = $('khungGioInput');
    if (kgInput) kgInput.value = _selectedKhungGioId;

    _setPanel(true);
    _capNhatGia();
    _capNhatDichVuInputs();
}

// ── Bấm +/- dịch vụ ─────────────────────────────────────────
function thayDoiSoLuong(id, delta) {
    id = String(parseInt(id));   // chuẩn hoá thành string để khớp key trong map
    if (!_dichVuSL.hasOwnProperty(id)) _dichVuSL[id] = 0;

    var newVal = Math.max(0, (_dichVuSL[id] || 0) + delta);
    _dichVuSL[id] = newVal;

    var qtyEl = document.getElementById('qty-' + id);
    if (qtyEl) qtyEl.textContent = newVal;

    _capNhatGia();
    _capNhatDichVuInputs();
}

// ── Tính & render toàn bộ panel giá ─────────────────────────
function _capNhatGia() {
    // 1. Tính tổng dịch vụ
    var tongDV = 0;
    Object.keys(_dichVuSL).forEach(function (id) {
        tongDV += (_dichVuSL[id] || 0) * (_dichVuMap[id] || 0);
    });

    // 2. Cập nhật dòng "Dịch vụ kèm"
    var rowDV = $('priceServiceRow');
    var valDV = $('priceServiceValue');
    var comboList = $('dvComboList');

    if (tongDV > 0) {
        if (rowDV) rowDV.style.display = 'block';

        var phatTraDV = Math.round(tongDV * _tyLeCoc);
        if (valDV) valDV.innerHTML =
            '<strong>' + _fmt(tongDV) + '</strong>' +
            ' <span style="font-size:12px;font-weight:500;color:var(--muted);">(ph\u1ea3i tr\u1ea3: <strong>' + _fmt(phatTraDV) + '</strong>)</span>';

        // Render từng dịch vụ ngay bên dưới
        if (comboList) {
            comboList.innerHTML = '';
            Object.keys(_dichVuSL).forEach(function (id) {
                var sl = _dichVuSL[id];
                if (!sl || sl <= 0) return;
                var gia = _dichVuMap[id] || 0;
                var qtyEl = document.getElementById('qty-' + id);
                var dvItem = qtyEl ? qtyEl.closest('.dv-item') : null;
                var nameEl = dvItem ? dvItem.querySelector('.dv-name') : null;
                var tenDV = nameEl ? nameEl.textContent.trim() : 'D\u1ecbch v\u1ee5';

                var row = document.createElement('div');
                row.style.cssText = 'display:flex;justify-content:space-between;font-size:13px;color:var(--muted);padding-left:8px;';
                row.innerHTML = '<span>' + tenDV + ' \u00d7' + sl + '</span><span style="color:var(--text2);font-weight:600;">' + _fmt(gia * sl) + '</span>';
                comboList.appendChild(row);
            });
        }
    } else {
        if (rowDV) rowDV.style.display = 'none';
    }

    // 3. Nếu chưa chọn slot → dừng
    if (!_selectedKhungGioId) return;

    // 4. Cập nhật label giá sân và tiền cọc sân cơ bản
    var tienCoc = Math.round(_selectedGia * _tyLeCoc);
    var pct = Math.round(_tyLeCoc * 100);

    var elGia = $('priceGia');
    var lblCoc = $('lblCoc');
    var elCoc = $('priceCoc');

    if (elGia) elGia.textContent = _fmt(_selectedGia);
    if (lblCoc) lblCoc.textContent = 'Tiền cọc (' + pct + '%)';
    if (elCoc) elCoc.textContent = _fmt(tienCoc);

    // Lưu tổng dịch vụ toàn cục để apDungVoucher tính đúng
    _tongDV = tongDV;

    // Tính lại tổng có xét voucher đang chọn (nếu có)
    if (typeof apDungVoucher === 'function') {
        apDungVoucher();
    } else {
        // Fallback khi apDungVoucher chưa load
        var elTotal = $('priceTotal');
        var conLaiRow = $('conLaiRow');
        var priceConLaiEl = $('priceConLai');
        var tongThanhToan = tienCoc + Math.round(tongDV * _tyLeCoc);
        var conLai = (_selectedGia + tongDV) - tongThanhToan;
        if (elTotal) elTotal.textContent = _fmt(tongThanhToan);
        if (conLai > 0 && conLaiRow) {
            conLaiRow.style.display = '';
            if (priceConLaiEl) priceConLaiEl.textContent = _fmt(conLai);
        }
    }
}

// ── Sync hidden inputs cho form POST ─────────────────────────
function _capNhatDichVuInputs() {
    var container = $('dichVuInputs');
    if (!container) return;
    container.innerHTML = '';

    Object.keys(_dichVuSL).forEach(function (id) {
        var sl = _dichVuSL[id];
        if (!sl || sl <= 0) return;
        var i1 = document.createElement('input');
        i1.type = 'hidden'; i1.name = 'dichVuIds'; i1.value = id;
        container.appendChild(i1);
        var i2 = document.createElement('input');
        i2.type = 'hidden'; i2.name = 'soLuongs'; i2.value = sl;
        container.appendChild(i2);
    });
}

// ── Show/hide panel booking ───────────────────────────────────
function _setPanel(show) {
    var noSlot = $('noSlotMsg');
    var selSlot = $('selectedSlot');
    var breakdown = $('priceBreakdown');
    var btn = $('btnDatSan');

    if (noSlot) noSlot.style.display = show ? 'none' : '';
    if (selSlot) selSlot.classList.toggle('show', show);
    if (breakdown) breakdown.classList.toggle('show', show);
    if (btn) btn.style.display = show ? 'block' : 'none';
}

// ── SignalR helpers ───────────────────────────────────────────
function _capNhatLuoi(slots) {
    if (!Array.isArray(slots)) return;
    slots.forEach(_capNhatSlot);
}

function _capNhatSlot(s) {
    var el = document.getElementById('kg-' + s.khungGioId);
    if (!el) return;

    el.dataset.status = s.trangThai;
    el.classList.remove('s-trong', 's-dadat', 's-giucho');

    var isDaDat = s.trangThai === 'DaDat';
    var isDangGiu = s.trangThai === 'DangGiu';
    var isTrong = !isDaDat && !isDangGiu;

    if (isDaDat) el.classList.add('s-dadat');
    else if (isDangGiu) el.classList.add('s-giucho');
    else el.classList.add('s-trong');

    var statusEl = el.querySelector('.gio-status');
    if (statusEl) {
        statusEl.textContent = isDaDat ? '\u0110\u00e3 \u0111\u1eb7t'
            : isDangGiu ? '\u0110ang gi\u1eef ch\u1ed7'
                : 'C\u00f2n tr\u1ed1ng';
        statusEl.className = 'gio-status ' +
            (isDaDat ? 's-dadat' : isDangGiu ? 's-giucho' : 's-trong');
    }

    // Khi slot tr\u1edf v\u1ec1 Trong: x\u00f3a daqua + countdown c\u0169, r\u1ed3i re-check th\u1eddi gian
    if (isTrong) {
        el.classList.remove('daqua');
        var cdWrap = el.querySelector('.countdown-wrap');
        if (cdWrap) cdWrap.remove();
        // capNhatDaQua \u0111\u01b0\u1ee3c \u0111\u1ecbnh ngh\u0129a trong Details.cshtml (global scope)
        if (typeof capNhatDaQua === 'function') capNhatDaQua();
    }

    if (isDaDat && parseInt(s.khungGioId) === _selectedKhungGioId) {
        _selectedKhungGioId = null;
        _setPanel(false);
        alert('Khung gi\u1edd b\u1ea1n \u0111ang ch\u1ecdn v\u1eeba b\u1ecb \u0111\u1eb7t! Vui l\u00f2ng ch\u1ecdn khung gi\u1edd kh\u00e1c.');
    }
}
