# コントリビューションガイドライン

Performance Monitor Analyzerプロジェクトへの貢献をご検討いただき、ありがとうございます。

## 貢献の方法

### 1. Issue報告
- バグを発見した場合や新機能の提案がある場合は、まずIssueを作成してください
- 適切なIssueテンプレートを使用してください
- 既存のIssueと重複していないか確認してください

### 2. プルリクエスト
- フォークしてブランチを作成してください
- 変更内容に応じて適切なブランチ名を付けてください（例：`feature/add-new-chart`, `fix/memory-leak`）
- PRテンプレートに従って詳細を記載してください

## 開発環境のセットアップ

### 必要な環境
- .NET 8.0 SDK
- Visual Studio 2022 または Visual Studio Code
- Git

### Dev Containerを使用する場合
```bash
# リポジトリをクローン
git clone https://github.com/MasayukiOzawa/PerformanceMonitorAnalyzer.git
cd PerformanceMonitorAnalyzer

# VS Codeでdev containerを起動
code .
# "Reopen in Container"を選択
```

### ローカル環境でのセットアップ
```bash
# 依存関係の復元
dotnet restore src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.csproj

# ビルド
dotnet build src/PerformanceMonitorAnalyzer/PerformanceMonitorAnalyzer.csproj

# テスト実行
dotnet test tests/PerformanceMonitorAnalyzer.Tests/PerformanceMonitorAnalyzer.Tests.csproj
```

## コーディング規約

### コードスタイル
- `.editorconfig`の設定に従ってください
- `dotnet format`でコードフォーマットを実行してください
- 日本語のコメントも歓迎します

### 命名規則
- C#の標準的な命名規則に従ってください
- PascalCase: クラス、メソッド、プロパティ
- camelCase: フィールド、ローカル変数
- 意味のある名前を使用してください

### コメント
- 複雑なロジックには日本語または英語でコメントを追加してください
- XML ドキュメントコメントを public メンバーに追加してください

## テスト

### テストの作成
- 新しい機能には必ずテストを追加してください
- 既存のテストが破損していないことを確認してください
- テストメソッド名は日本語も使用可能です

### テストの実行
```bash
# 全てのテスト実行
dotnet test

# カバレッジレポート生成
dotnet test --collect:"XPlat Code Coverage"
```

## ブランチ戦略

- `main`: 安定版リリース用
- `release`: リリース準備用
- `feature/*`: 新機能開発用
- `fix/*`: バグ修正用
- `copilot/*`: Copilot開発用

## プルリクエストのガイドライン

### 前提条件
- [ ] 関連するIssueが存在する（新機能の場合）
- [ ] 適切なブランチから作成されている
- [ ] コードがフォーマットされている
- [ ] テストが通る
- [ ] コンフリクトがない

### レビュープロセス
1. 自動チェック（CI/CD）が通ることを確認
2. コードレビューを受ける
3. フィードバックに対応
4. 承認後、マージ

### マージ条件
- 最低1名の承認が必要
- 全てのチェックが通る
- コンフリクトが解決されている

## リリースプロセス

1. `release`ブランチで変更をまとめる
2. バージョン番号を更新
3. GitHub Actionsで`main`ブランチにマージ
4. 自動的にタグとリリースが作成される

## 質問・サポート

- 開発に関する質問は、Issueまたはディスカッションで行ってください
- セキュリティに関する問題は、SECURITY.mdに従って報告してください

## 行動規範

- 敬意を持って他の貢献者と接してください
- 建設的なフィードバックを心がけてください
- 多様性を尊重してください

## ライセンス

貢献したコードは、プロジェクトのライセンス（MIT）の下で公開されることに同意したものとみなします。

---

ご質問やご不明な点がございましたら、お気軽にIssueを作成してください。貢献をお待ちしています！