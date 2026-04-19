# 登入邏輯分析 (參考 APiAuth 專案)

此文件分析了 `APiAuth` 專案中的登入處理流程。雖然 `winSleep` 本身不包含登入功能，但此文件可作為 workspace 中相關功能的參考。

## 1. 登入流程說明

### 前端 (Login.cshtml)
- 使用 **jQuery AJAX** 發送 `POST` 請求到 `/Account/Login`。
- 傳送資料包含 `Username` 與 `Password`。
- 根據伺服器回傳的 JSON 結果 (`success`, `message`) 更新 UI。

### 後端 (AccountController.cs)
- **驗證**: 檢查帳號是否為 `admin` 且密碼為 `password123`。
- **授權**: 驗證成功後，調用 `FormsAuthentication.SetAuthCookie` 核發身份驗證 Cookie (通行證)。
- **回應**: 回傳 JSON 格式的成功或失敗訊息。

## 2. 登入時序圖 (Sequence Diagram)

```mermaid
sequenceDiagram
    participant U as 使用者
    participant B as 瀏覽器 (Login.cshtml)
    participant C as AccountController
    participant F as FormsAuthentication
    participant S as 伺服器 Session/Cookie

    U->>B: 輸入帳號密碼並點擊登入
    B->>C: AJAX POST /Account/Login (model)
    
    Note over C: 驗證帳密 (admin / password123)
    
    alt 驗證成功
        C->>F: SetAuthCookie(username, persistent=false)
        F->>S: 產生並核發 .ASPXAUTH Cookie
        C-->>B: 回傳 { success: true, message: "登入成功" }
        B->>U: 顯示成功訊息 (由 AJAX 回調處理)
    else 驗證失敗
        C-->>B: 回傳 { success: false, message: "帳號或密碼錯誤" }
        B->>U: 顯示錯誤訊息
    end
```

## 3. 登出流程 (Logout)

```mermaid
sequenceDiagram
    participant U as 使用者
    participant B as 瀏覽器
    participant C as AccountController
    participant F as FormsAuthentication

    U->>B: 點擊登出
    B->>C: AJAX POST /Account/Logout
    C->>F: SignOut()
    Note over F: 撤銷/清除 .ASPXAUTH Cookie
    C-->>B: 回傳 { success: true, message: "已成功登出" }
    B->>U: 更新 UI 狀態
```
