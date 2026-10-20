> 各層のボス階の設計図。保存すると取り込まれ、各層のボス階の設定（Floor_L{n}_Boss_Config）に入る（Tools/TPS Dungeon/ボス階の設計図を取り込む でも取り込める）。
> - 1 文字が 1 セル（5 m 四方）。上が北。同じ文字のかたまりが 1 つの部屋で、四角形にする。`.` は何も無い所。
> - `A` ボス部屋（5x5）、`H` 大扉の手前の部屋（大きさは自由）、`X` 出口（1x1。ボス部屋に接する）、`E` 入口（1x1）、`S` ショートカットの部屋（1x1）。小文字は道のりの部屋（大きさは自由）。
> - 扉: `a-b` で a と b の接している辺の真ん中に扉を開ける。`H-A`（大扉）と `A-X`（ボスを倒すまで閉ざされた扉）は書かなくても付く。`S` の扉を書かなければ隣の `H` と繋ぐ。
> - 敵: 書いた部屋にだけ、その層のランダムな階と同じ表で敵を出す。
> - `>` で始まる行は説明として読み飛ばす。

# 第1層
...X...
.AAAAA.
.AAAAA.
.AAAAA.
.AAAAA.
.AAAAA.
..HHHS.
...E...
扉: E-H
敵: H

# 第2層
......X......
....AAAAA....
....AAAAA....
....AAAAA....
....AAAAA....
...EAAAAAS...
.aaaaHHHcccc.
.aaaa...cccc.
扉: E-a a-H c-H c-S
敵: a c H

# 第3層
....X....
..AAAAA..
..AAAAA..
..AAAAA..
..AAAAA..
..AAAAA..
..HHHHH..
..HHHHH..
.EHHHHHS.
..HHHHH..
..HHHHH..
扉: E-H
敵: H

# 第4層
.aaaa...dddd.
.aaaaEXSdddd.
...bAAAAAc...
...bAAAAAc...
...bAAAAAc...
...bAAAAAc...
...bAAAAAc...
...bHHHHHc...
扉: E-a a-b b-H H-c c-d d-S
敵: a b c d H

# 第5層
...X......
.AAAAA....
.AAAAA....
.AAAAA....
.AAAAAaaS.
.AAAAAaaE.
..HHHHbbb.
..HHHHbbb.
..HHHHbbb.
..HHHH....
扉: E-a a-b b-H a-S
敵: a b H