using Web_Stadium.EFCore;

namespace Web_Stadium.Services
{
    public class ScheduleService
    {
        public class SlotKhungGio
        {
            public int KhungGioId { get; set; }
            public DateTime Ngay { get; set; }
        }

        public List<TranDau> SinhLichVongTron(GiaiDau giaiDau, List<SlotKhungGio>? lichBlock = null)
        {
            var tatCaTran = new List<TranDau>();
            var ngayDau = giaiDau.NgayBatDau;

            foreach (var bang in giaiDau.BangDaus)
            {
                var doiTrongBang = giaiDau.DoiBongs
                    .Where(d => d.BangId == bang.Id && d.DaThanhToan)
                    .ToList();

                if (doiTrongBang.Count < 2) continue;

                var lichBang = SinhLichBerger(doiTrongBang, bang.Id, giaiDau.Id, ngayDau);
                tatCaTran.AddRange(lichBang);
            }

            // Nếu có lịch slot Owner đã block — phân phối trận theo thứ tự thời gian
            if (lichBlock != null && lichBlock.Count > 0)
            {
                var slotSorted = lichBlock
                    .OrderBy(s => s.Ngay)
                    .ThenBy(s => s.KhungGioId)
                    .ToList();

                var tranSorted = tatCaTran
                    .OrderBy(t => t.VongDau)
                    .ThenBy(t => t.BangId)
                    .ToList();

                int n = Math.Min(slotSorted.Count, tranSorted.Count);
                for (int i = 0; i < n; i++)
                {
                    tranSorted[i].KhungGioId = slotSorted[i].KhungGioId;
                    tranSorted[i].NgayThiDau = slotSorted[i].Ngay;
                }
            }

            return tatCaTran;
        }

        private List<TranDau> SinhLichBerger(
            List<DoiBong> dsDoi, int bangId, int giaiDauId, DateTime ngayDau)
        {
            var result = new List<TranDau>();

            var ds = new List<DoiBong?>(dsDoi.Cast<DoiBong?>());
            if (ds.Count % 2 != 0) ds.Add(null);

            int n = ds.Count;
            int soVong = n - 1;
            int soTranMoiVong = n / 2;

            for (int vong = 0; vong < soVong; vong++)
            {
                var ngayVong = ngayDau.AddDays(vong * 7);

                for (int slot = 0; slot < soTranMoiVong; slot++)
                {
                    var doiNha = ds[slot];
                    var doiKhach = ds[n - 1 - slot];

                    if (doiNha == null || doiKhach == null) continue;

                    bool doiNhaThuat = (vong + slot) % 2 == 0;
                    result.Add(new TranDau
                    {
                        GiaiDauId = giaiDauId,
                        BangId = bangId,
                        DoiNhaId = doiNhaThuat ? doiNha.Id : doiKhach.Id,
                        DoiKhachId = doiNhaThuat ? doiKhach.Id : doiNha.Id,
                        VongDau = vong + 1,
                        LoaiVong = "VongBang",
                        NgayThiDau = ngayVong,
                        TrangThai = "Scheduled"
                    });
                }

                var last = ds[n - 1];
                for (int i = n - 1; i > 1; i--)
                    ds[i] = ds[i - 1];
                ds[1] = last;
            }

            return result;
        }
    }
}
