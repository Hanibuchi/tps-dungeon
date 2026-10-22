using System.Collections;
using System.Collections.Generic;
using TpsDungeon.Items;

namespace TpsDungeon.Party
{
    /// <summary>
    /// 転生の渦に入ったあと、持ち越す武器を選ばせる画面。画面は Menu の側にあるので、<see cref="GameFlow.CarryOverPicker"/> に自分を入れておく。
    /// </summary>
    public interface ICarryOverPicker
    {
        /// <summary>
        /// 選ばせる。limit 本まで選べ、決めたら終わる。選んだ物を keep に入れる。
        /// 呼ばれたときの画面は真っ白（転生の演出が白く飛ばしたところ）なので、白から出て、終わるときは白へ戻す。
        /// </summary>
        IEnumerator Pick(Party party, int limit, ICollection<ItemInstance> keep);
    }
}
