# 車体に合わせるカメラ

実施日: 2026-10-02。カメラの位置と向きを車体へ追従させました。砲塔が先に回る間は車体の正面を見るようにし、車体の旋回後は滑らかに追従します。砲撃・ビームと照準は砲塔に追従します。

砲塔用のカメラ参照を削除し、通常の追従と復帰時のSnapで同じ車体方向を使います。カメラの高さ2.4・後方2.2・視野角72度、障害物による距離の調整、巨大化時の距離倍率、地形に沿った傾斜は維持しています。

## 検証

- 関連PlayModeテスト **2件成功・0件失敗、2.22秒**。ログとXMLは `Logs/hull-camera-tests.log` / `Logs/hull-camera-tests.xml`。
- 初期位置のカメラ距離・高さ・視野角を検証。砲塔の先行旋回中でもSnapは車体に向き、通常の追従中も車体の向きを見ることを確認しました。
- 実際の砲弾が砲塔の方向へ飛ぶ検証も成功。照準は従来の砲口から70先を投影する処理を使います。
- Windows x64 Development Build成功。ログは `Logs/hull-camera-build.log`。

Windows / Direct3D 11で1440×900の3画面を描画確認しました。砲塔が12.6度向きを変えた時点でもカメラと車体の水平角の差は0度。旋回中は滑らかな追従による差が3.40度、ハンドルを戻した後は0.02度でした。車体後方から砲塔が左右に動く様子と、車体の旋回に合わせた街の見え方を確認しています。最終ビルド・実行ログにゲーム例外・シェーダーエラー・C#コンパイルエラーはありません。ログは `Logs/hull-camera-player.log`。

    CAMERA SMOKE COMPLETE: turretLead=12.60, earlyHull=0.00,
    earlyCameraHullAngle=0.00, earlyCameraTurretAngle=12.61,
    followCameraHullAngle=3.40, centeredCameraHullAngle=0.02, crashes=0

![車体方向を向き、砲塔だけが先に旋回](images/tank-town-camera-hull.png)

Windows版を `Builds/TankTown/TankTown.exe` へ反映し、検証コピーと配置先のRuntime DLLのSHA-256一致を確認済みです。3画面は `Builds/TankTown/Captures/camera-*.png` に保存しました。

    84183867C659889DCC60D25797DE0062D0305DD517BEC680DB886E5802353980

Unity 6000.3.19f1 / URP 17.3.0。独立した検証コピーを使用。今回の変更に関連する2件を実行し、アイテム実装時の39件の検証記録は [items-validation.md](items-validation.md) に残しています。ゲームがネイティブUnityのため、画面確認はWindowsプレイヤーを使います。formatter/lint専用設定はなく、コンパイルとGit差分チェックを実施しました。
