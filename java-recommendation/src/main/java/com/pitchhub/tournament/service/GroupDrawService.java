package com.pitchhub.tournament.service;

import com.pitchhub.tournament.dto.DrawRequest;
import com.pitchhub.tournament.dto.DrawResponse;
import com.pitchhub.tournament.dto.GroupAssignmentDto;
import com.pitchhub.tournament.dto.TeamDto;
import org.springframework.stereotype.Service;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;
import java.util.Random;

/**
 * Bốc thăm chia bảng bằng thuật toán "snake seeding" (rắn bò).
 *
 * Tại sao snake seeding:
 *   - Đơn giản, không cần bảng xếp hạng phức tạp.
 *   - Chia đội theo sức mạnh (seedRating) đều đặn qua các bảng: đội hạt giống #1
 *     xuống bảng 0, #2 → bảng 1, ..., đến bảng cuối thì đổi chiều đi ngược lại.
 *   - Nếu tất cả seedRating = 0 (không có hạt giống) → shuffle thuần, vẫn cân
 *     bằng số đội mỗi bảng.
 *
 * Random seed = giaiId để cùng giải gọi 2 lần cho cùng kết quả (idempotent).
 */
@Service
public class GroupDrawService {

    public DrawResponse draw(DrawRequest req) {
        DrawResponse out = new DrawResponse();

        int soBang = Math.max(1, req.getSoBang());
        List<TeamDto> teams = new ArrayList<>(req.getTeams());

        if (teams.isEmpty()) {
            out.getWarnings().add("Không có đội nào để chia bảng.");
            return out;
        }
        if (teams.size() < soBang) {
            out.getWarnings().add("Số đội (" + teams.size()
                    + ") ít hơn số bảng (" + soBang + ") — sẽ có bảng trống.");
        }

        // 1) Sắp theo seedRating giảm dần (0 = không seed, đi cuối)
        //    rồi shuffle deterministic bên trong các đội cùng rating để
        //    tránh cùng cặp đội mạnh luôn rơi vào bảng đầu.
        Random rand = new Random(req.getGiaiId());
        teams.sort(Comparator.comparingInt(TeamDto::getSeedRating).reversed());
        shuffleSameRating(teams, rand);

        // 2) Snake seeding: 0,1,2,...,soBang-1, soBang-1,...,1,0, 0,1,...
        for (int i = 0; i < teams.size(); i++) {
            int cyclePos = i % (soBang * 2);
            int groupIndex = cyclePos < soBang ? cyclePos : (soBang * 2 - 1 - cyclePos);
            out.getAssignments().add(
                    new GroupAssignmentDto(teams.get(i).getId(), groupIndex));
        }

        // 3) Cảnh báo lệch: bảng lớn nhất/nhỏ nhất chênh > 1 đội
        int[] count = new int[soBang];
        for (GroupAssignmentDto a : out.getAssignments()) count[a.getGroupIndex()]++;
        int min = Integer.MAX_VALUE, max = Integer.MIN_VALUE;
        for (int c : count) { if (c < min) min = c; if (c > max) max = c; }
        if (max - min > 1) {
            out.getWarnings().add("Các bảng lệch " + (max - min) + " đội (min="
                    + min + ", max=" + max + ").");
        }

        return out;
    }

    /** Shuffle các đoạn liên tiếp có cùng seedRating để bốc thăm ngẫu nhiên trong nhóm hạt giống. */
    private void shuffleSameRating(List<TeamDto> teams, Random rand) {
        int i = 0;
        while (i < teams.size()) {
            int j = i;
            while (j < teams.size() && teams.get(j).getSeedRating() == teams.get(i).getSeedRating()) {
                j++;
            }
            Collections.shuffle(teams.subList(i, j), rand);
            i = j;
        }
    }
}
