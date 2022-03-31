using MSTSCLib;
using System.Text.Json.Serialization;

namespace ManagedMstsc
{
    public class ResultEntity
    {
        [JsonPropertyName("disconnectReason")]
        public int DisconnectReason { get; set; }

        [JsonIgnore]
        public ExtendedDisconnectReasonCode ExtendedDisconnectReason { get; set; }


        [JsonPropertyName("extendedDisconnectReason")]
        public string ExtendedDisconnectReasonString
        {
            get
            {
                return ExtendedDisconnectReason.ToString();
            }
        }

        /// <summary>
        /// 切断理由を取得します。
        /// </summary>
        /// <remarks>
        /// <see cref="DisconnectReason"/> および <see cref="ExtendedDisconnectReason"/> が 0 であっても、
        /// 本値が <see cref="string.IsNullOrEmpty(string?)"/> でない場合はエラーとして扱います。
        /// </remarks>
        [JsonPropertyName("disconnectReasonString")]
        public string DisconnectReasonString { get; set; } = null;

        [JsonPropertyName("isError")]
        public bool IsError
        {
            get
            {
                // DisconnectReason
                // 516 reason in case of reconnect expired
                // 2308 connection lost
                // 2 - regular logof also in case of forced reboot or shutdown

                return (string.IsNullOrEmpty(DisconnectReasonString) == false) || (DisconnectReason != 2);
            }
        }
    }
}
