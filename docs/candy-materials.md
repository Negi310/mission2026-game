# お菓子の街の素材一覧

2026-10-10。ユーザー添付の街の画像を素材・配色の参考にして、10種類の画像テクスチャを制作し、Unityの実際の街へ適用しました。形状で気泡・種・格子・縞を増やす方式ではなく、テクスチャの色と微細な陰影で質感を表現します。

|素材|使う場所|画像を参考に追加したディテール|テクスチャ|
|---|---|---|---|
|スポンジ|ショートケーキの外壁|淡い黄色の生地、不規則な気泡、しっとりした細かな粒|[Sponge.png](../Assets/TankTown/Resources/CandyTextures/Sponge.png)|
|ウエハース|チョコ・ビスケットの柱、ビスケットの屋根、ゲートの支柱、カップケーキの台|斜めの格子、焼き色、格子の溝の陰影|[Wafer.png](../Assets/TankTown/Resources/CandyTextures/Wafer.png)|
|ビスケット|ビスケットの外壁・看板・歩道|小さな穴、焼けた斑点、粉の粒子感|[Biscuit.png](../Assets/TankTown/Resources/CandyTextures/Biscuit.png)|
|板チョコ|チョコの壁、浮き出たチョコ片、窓、ベンチ|カカオ色の細かな粒、控えめな表面のむら、滑らかな光沢|[Chocolate.png](../Assets/TankTown/Resources/CandyTextures/Chocolate.png)|
|ホイップクリーム|ケーキの縁、床の層、屋上、キャンディの棒|アイボリーの濃淡、柔らかい筋、幅の広い穏やかなハイライト|[Cream.png](../Assets/TankTown/Resources/CandyTextures/Cream.png)|
|糖衣・ジャム|報酬色の帯、ドーナツのピンクの糖衣、屋上の菓子、カップケーキ、赤いジャム・チェリー|滑らかな糖衣、細かな流れ模様、つや。白い共通画像に用途別の色を掛ける|[Glaze.png](../Assets/TankTown/Resources/CandyTextures/Glaze.png)|
|イチゴ|ケーキの屋上|赤い果皮、金色の種、種の周りのくぼみ、果皮の細かな粒|[Strawberry.png](../Assets/TankTown/Resources/CandyTextures/Strawberry.png)|
|キャンディケーン|街灯と看板の棒・曲がり部分|斜めの赤白模様、砂糖の細かな粒、硬い飴のつや|[CandyStripe.png](../Assets/TankTown/Resources/CandyTextures/CandyStripe.png)|
|ロリポップ|街路樹・広場の棒付き飴|平面の渦巻き、細かな砂糖の光沢。共通画像を6色などに着色|[CandySwirl.png](../Assets/TankTown/Resources/CandyTextures/CandySwirl.png)|
|ドーナツ生地|壊せるドーナツゲート|黄金色の焼きむら、細かな生地の穴、柔らかい表面の陰影|[Dough.png](../Assets/TankTown/Resources/CandyTextures/Dough.png)|

イチゴの葉にも、共通の糖衣テクスチャを緑に着色した穏やかな光沢の材質を使用します。車・戦車・道路・UIの材質は従来のものを使います。建物の帯の色とスペシャルアイテムの対応を維持しています。

## シルエットと破壊

参考画像へ建物のディテールを近づけた調整は[建物の詳細と検証](candy-building-details-validation.md)を参照してください。ケーキは2階分を1段として白いクリームで囲み、段の縁にもイチゴを並べます。チョコの壁には厚い角丸の縁と浅いくぼみ、ビスケットの家には縞のひさし、カップケーキには重なったクリームを追加しています。

丸い果実・飴・ドーナツは共有メッシュの滑らかな法線と輪郭を使用します。クリームの波打つ縁と街灯の曲がった棒も、シルエットに必要な形だけを作ります。イチゴの種、飴の縞、ロリポップの渦巻き、ウエハースの細い溝を個別の立体で足す処理は除去しました。

遠景用の建物でも、イチゴ・丸い飴・ドーナツは箱に置き換えず、輪郭が残る共有メッシュを使います。遠いイチゴと飴の頂点数は近景より減らします。壁・柱・床の遠景モデル、材質別のバッチ、通常の街の簡略化は維持しています。

柱・梁・床の支持、部材の耐久、砲撃の威力、ゲートの破壊判定、全壊後のアイテムエリアは既存の仕組みを使います。大きな板を割った破片には、その寸法に合わせた共有メッシュとメートル単位のUVを使い、元の板の模様全体が小さな破片に縮められることを防ぎます。材質と画像は共有し、破片のためにテクスチャを複製しません。

## 画像生成と読み込み

組み込みの`image_gen`で素材ごとに1回ずつ生成しています。[参考画像](../output/imagegen/candy-texture-reference-20261010.png)は素材と雰囲気の参照です。生成後に街のスクリーンショットを加工する方式ではありません。[実際の生成プロンプト10件](../output/imagegen/candy-texture-prompts-20261010.json)を保存しています。

最終画像は`Assets/TankTown/Resources/CandyTextures/`へ保存し、素材の貼り方・光沢・微細な凹凸は`TankTownCandyWorld.cs`で設定します。URPシェーダーが画像を1回サンプリングし、その明暗の微分から小さな凹凸の陰影を加えます。質感のための頂点変位や物理コライダーは追加しません。UVを建物の材質別バッチにも保持します。

Unityでは各画像を最大1024、BC7圧縮、ミップマップ、Trilinear、異方性4で取り込みます。渦巻きはClamp、その他はRepeatです。通常の街にはテクスチャ用のシェーダーバリアントを使いません。
