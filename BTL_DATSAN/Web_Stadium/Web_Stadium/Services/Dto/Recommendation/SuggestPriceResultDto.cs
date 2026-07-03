namespace Web_Stadium.Services.Dto.Recommendation
{
    /// <summary>
    /// Khop dung JSON that tra ve tu POST /api/owner/suggest-price (java-recommendation) —
    /// da kiem chung truc tiep qua curl: {"suggestedGoldPrice","suggestedRegularPrice","reason","confidence"}.
    /// KHONG phai {GiaVang, GiaThuong, MoTa} nhu mo ta ban dau — doi ten cho dung API that.
    /// </summary>
    public class SuggestPriceResultDto
    {
        public int SuggestedGoldPrice { get; set; }
        public int SuggestedRegularPrice { get; set; }
        public string? Reason { get; set; }
        public double Confidence { get; set; }
    }
}
