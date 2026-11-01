# レーザー反動を小刻みにする調整（2026-10-25）

左右の主成分を6→12Hz、補助成分を9→15Hz、上下を7→14Hzへ変更しました。標準振幅0.28mは維持し、揺り返しの大きさを変えずに細かく震える見た目にしています。立ち上がり0.18秒・減衰0.24秒、位置だけに反動を加える構造、一時停止・中断・Snapの処理も維持しています。[前の振幅強化](beam-recoil-stronger-validation.md)。

既存のレーザー関連5項目のPlayModeテストは全て成功。カメラと反動なしの比較カメラの位置差から、振幅・角度・画角・追従への蓄積・停止後の復帰を確認しました。水平の正負の切り替わりが36回以上あることも確認し、前のゆっくりした揺れに戻る変更を検出します。周波数変更に合わせて、60fps時の反動のフレーム差の上限を0.35mに調整しました。

記録：`Logs/beam-recoil3-tests.xml`、`Logs/beam-recoil3-tests.log`。

Windows Development Build成功。通常街33棟・大型街132棟とも終了コード0。138フレーム（60fps固定刻み）で水平方向の正負が53回切り替わり、反動の最大幅0.26675m、フレーム差の最大0.31408mでした。振幅は前回の0.26389mと同程度で、振動を速くした調整です。

1920×1080換算の参照点の投影変化は建物位置5.51px・戦車位置15.51px。画角72度、終了後の反動ゼロを維持。専用視点への移行を含むカメラ全体の最大フレーム移動は0.546m、角度変化は0.388度です。レーザーの命中数は通常163・大型327でした。

通常の`Builds/TankTown/TankTown.exe`と`Builds/TankTown-FastBeamRecoil/TankTown.exe`を更新。RuntimeアセンブリのSHA-256は両方とも`911EFC6759BC4805B0A3C9F2C829362EAE0FD6A44278BC1325D8D367A6B6DA10`です。

検証コピーとC# 109ファイルが改行正規化後に一致。利用者の描画設定・撮影コード9ファイルは作業前のハッシュと一致し、既存の削除3ファイルも復元していません。既存のAGENTSや開発手順の更新は不要です。

記録：`Logs/beam-recoil3-build.log`、`beam-recoil3-{town,big}-player.log`、`beam-recoil3-source-check.json`、`beam-recoil3-local-overlays-check.json`、`beam-recoil3-build-hashes.json`。画像は`Logs/beam-recoil3-{town,big}-beam-camera-*.png`です。投影値は診断配置のもので、個人の体感評価を測定した試験ではありません。
