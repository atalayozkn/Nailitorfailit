using UnityEngine;

public class PlayerShopState : PlayerBaseState
{
    private GameObject shopCamera;

    // PlayerShopState oluþturulduðunda çalýþýr.
    // PlayerStateMachine referansýný base PlayerBaseState'e gönderir.
    public PlayerShopState(PlayerStateMachine stateMachine) : base(stateMachine)
    {
    }

    // Shop state'ine girildiðinde çalýþýr.
    // Shop Camera referansýný alýr, Player kontrollerini kapatýr ve Shop Camera'yý aktif eder.
    public override void Enter()
    {
        shopCamera = stateMachine.currentShopCamera;

        SetPlayerActivity(false);
        SetShopCameraActive(true);
    }

    // Shop state'i aktifken her frame çaðrýlýr.
    // Shop sýrasýnda frame bazlý bir iþlem olmadýðý için boþ býrakýlmýþtýr.
    public override void Tick(float deltaTime)
    {
        // Shop açýkken frame bazlý iþlem yok.
    }

    // Shop state'i aktifken fizik güncellemelerinde çaðrýlýr.
    // Shop sýrasýnda fizik bazlý bir iþlem olmadýðý için boþ býrakýlmýþtýr.
    public override void FixedTick(float fixedDeltaTime)
    {
        // Shop açýkken fizik bazlý iþlem yok.
    }

    // Shop state'inden çýkýldýðýnda çalýþýr.
    // Shop Camera'yý kapatýr, Player ölü deðilse kontrolleri tekrar açar ve kamera referansýný temizler.
    public override void Exit()
    {
        SetShopCameraActive(false);

        if (stateMachine.currentPlayerState != PlayerStates.Dead)
        {
            SetPlayerActivity(true);
        }

        shopCamera = null;
    }

    #region PLAYER ACTIVITY

    // Player'ýn hareket ve etkileþim sistemlerini aktif veya pasif hale getirir.
    // PlayerMovement ve PlayerInteractionHandler içerisindeki SetActivity() fonksiyonlarýný çalýþtýrýr.
    private void SetPlayerActivity(bool condition)
    {
        if (stateMachine.movementHandler != null)
        {
            stateMachine.movementHandler.SetActivity(condition);
        }

        if (stateMachine.interactionHandler != null)
        {
            stateMachine.interactionHandler.SetActivity(condition);
        }
    }

    #endregion

    #region CAMERA

    // Shop Camera'nýn aktif veya pasif olmasýný saðlar.
    // Kamera zaten istenen durumdaysa iþlem yapmaz, farklý durumdaysa SetActive() çaðýrýr.
    private void SetShopCameraActive(bool condition)
    {
        if (shopCamera == null)
        {
            return;
        }

        if (shopCamera.activeSelf == condition)
        {
            return;
        }

        shopCamera.SetActive(condition);
    }

    #endregion
}