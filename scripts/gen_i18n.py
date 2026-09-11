# -*- coding: utf-8 -*-
"""i18n 资源生成器：单一数据源生成 Strings/<lang>/Resources.resw。

用法: python scripts/gen_i18n.py
- 每个键给出 13 种语言翻译；语言缺失某键时 MRT 回退默认语言 en-US，
  全部缺失时 L10n.T 回退资源键。
"""
import io, os

# (key, zh-CN, en, zh-TW, ja, ko, de, es, ru, tr, id, fa, ar, tt)
T = []

def k(key, zh, en, zht, ja, ko, de, es, ru, tr, idn, fa, ar, tt):
    T.append((key, zh, en, zht, ja, ko, de, es, ru, tr, idn, fa, ar, tt))

# ---------- 导航 ----------
k("Nav_Home", "首页", "Home", "首頁", "ホーム", "홈", "Start", "Inicio", "Главная", "Ana Sayfa", "Beranda", "خانه", "الرئيسية", "Баш бит")
k("Nav_Proxies", "代理", "Proxies", "代理", "プロキシ", "프록시", "Proxys", "Proxies", "Прокси", "Proxyler", "Proxy", "پروکسی‌ها", "البروكسي", "Прокси")
k("Nav_Profiles", "订阅", "Profiles", "訂閱", "サブスクリプション", "구독", "Profile", "Perfiles", "Подписки", "Profiller", "Profil", "پروفایل‌ها", "الملفات الشخصية", "Профильләр")
k("Nav_Connections", "连接", "Connections", "連接", "接続", "연결", "Verbindungen", "Conexiones", "Соединения", "Bağlantılar", "Koneksi", "اتصال‌ها", "الاتصالات", "Тоташулар")
k("Nav_Rules", "规则", "Rules", "規則", "ルール", "규칙", "Regeln", "Reglas", "Правила", "Kurallar", "Aturan", "قوانین", "القواعد", "Кагыйдәләр")
k("Nav_Logs", "日志", "Logs", "日誌", "ログ", "로그", "Protokolle", "Registros", "Журналы", "Kayıtlar", "Log", "گزارش‌ها", "السجلات", "Журналлар")
k("Nav_Unlock", "解锁测试", "Unlock Test", "解鎖測試", "アンロックテスト", "잠금 해제 테스트", "Entsperrtest", "Prueba de desbloqueo", "Тест разблокировки", "Kilit Açma Testi", "Uji Buka Kunci", "تست رفع انسداد", "اختبار إلغاء القفل", "Ачуды тикшерү")
k("Nav_Settings", "设置", "Settings", "設定", "設定", "설정", "Einstellungen", "Ajustes", "Настройки", "Ayarlar", "Pengaturan", "تنظیمات", "الإعدادات", "Көйләүләр")

# ---------- 通用 ----------
k("Common_OK", "确定", "OK", "確定", "OK", "확인", "OK", "Aceptar", "OK", "Tamam", "OK", "تأیید", "موافق", "Тамам")
k("Common_Cancel", "取消", "Cancel", "取消", "キャンセル", "취소", "Abbrechen", "Cancelar", "Отмена", "İptal", "Batal", "لغو", "إلغاء", "Баш тарту")
k("Common_Save", "保存", "Save", "儲存", "保存", "저장", "Speichern", "Guardar", "Сохранить", "Kaydet", "Simpan", "ذخیره", "حفظ", "Саклау")
k("Common_Delete", "删除", "Delete", "刪除", "削除", "삭제", "Löschen", "Eliminar", "Удалить", "Sil", "Hapus", "حذف", "حذف", "Бетерү")
k("Common_Close", "关闭", "Close", "關閉", "閉じる", "닫기", "Schließen", "Cerrar", "Закрыть", "Kapat", "Tutup", "بستن", "إغلاق", "Ябу")
k("Common_Apply", "应用", "Apply", "應用", "適用", "적용", "Anwenden", "Aplicar", "Применить", "Uygula", "Terapkan", "اعمال", "تطبيق", "Куллану")
k("Common_Refresh", "刷新", "Refresh", "重新整理", "更新", "새로 고침", "Aktualisieren", "Actualizar", "Обновить", "Yenile", "Segarkan", "بازخوانی", "تحديث", "Яңарту")

# ---------- 首页 ----------
k("Home_Title", "首页", "Home", "首頁", "ホーム", "홈", "Start", "Inicio", "Главная", "Ana Sayfa", "Beranda", "خانه", "الرئيسية", "Баш бит")
k("Home_OpenProxySettings", "进入代理设置", "Proxy settings", "進入代理設定", "プロキシ設定", "프록시 설정", "Proxy-Einstellungen", "Ajustes de proxy", "Настройки прокси", "Proxy ayarları", "Pengaturan proxy", "تنظیمات پروکسی", "إعدادات البروكسي", "Прокси көйләүләре")
k("Home_CardNetwork", "网络控制", "Network", "網路控制", "ネットワーク", "네트워크", "Netzwerk", "Red", "Сеть", "Ağ", "Jaringan", "شبکه", "الشبكة", "Челтәр")
k("Home_TunAdminHint", "TUN 需要管理员权限运行", "TUN requires administrator", "TUN 需要管理員權限執行", "TUN には管理者権限が必要です", "TUN에는 관리자 권한이 필요합니다", "TUN erfordert Administratorrechte", "TUN requiere permisos de administrador", "TUN требует прав администратора", "TUN yönetici hakkı gerektirir", "TUN membutuhkan hak administrator", "TUN به دسترسی مدیر نیاز دارد", "يتطلب TUN صلاحيات المسؤول", "TUN өчен администратор хоккы кирәк")
k("Home_CardProfile", "当前订阅", "Current profile", "目前訂閱", "現在のサブスク", "현재 구독", "Aktuelles Profil", "Perfil actual", "Текущая подписка", "Mevcut profil", "Profil saat ini", "پروفایل فعلی", "الملف الحالي", "Хәзерге профиль")
k("Home_CardNodes", "当前节点", "Current node", "目前節點", "現在のノード", "현재 노드", "Aktueller Knoten", "Nodo actual", "Текущий узел", "Mevcut düğüm", "Node saat ini", "گره فعلی", "العقدة الحالية", "Хәзерге төен")
k("Home_CardTraffic", "流量统计", "Traffic", "流量統計", "トラフィック統計", "트래픽 통계", "Traffic", "Tráfico", "Трафик", "Trafik", "Statistik trafik", "آمار ترافیک", "إحصاءات حركة البيانات", "Трафик статистикасы")
k("Home_CardMode", "运行模式", "Mode", "執行模式", "動作モード", "실행 모드", "Modus", "Modo", "Режим", "Mod", "Mode", "حالت اجرا", "وضع التشغيل", "Эш режимы")
k("Home_CardCore", "内核信息", "Core info", "核心資訊", "コア情報", "코어 정보", "Kern-Info", "Info del núcleo", "О ядре", "Çekirdek bilgisi", "Info core", "اطلاعات هسته", "معلومات النواة", "Йөзәк турында")

# ---------- 代理页 ----------
k("Proxies_Title", "代理", "Proxies", "代理", "プロキシ", "프록시", "Proxys", "Proxies", "Прокси", "Proxyler", "Proxy", "پروکسی‌ها", "البروكسي", "Прокси")
k("Proxies_FilterPlaceholder", "筛选节点名称/类型", "Filter by name/type", "篩選節點名稱/類型", "名前/タイプで絞り込み", "이름/유형 필터", "Nach Name/Typ filtern", "Filtrar por nombre/tipo", "Фильтр по имени/типу", "Ada/türe göre filtrele", "Filter nama/tipe", "فیلتر نام/نوع", "تصفية بالاسم/النوع", "Исем/тип буенча фильтр")
k("Proxies_Empty", "暂无代理组", "No proxy groups", "暫無代理群組", "プロキシグループなし", "프록시 그룹 없음", "Keine Proxy-Gruppen", "Sin grupos de proxy", "Нет групп прокси", "Proxy grubu yok", "Tidak ada grup proxy", "بدون گروه پروکسی", "لا مجموعات بروكسي", "Прокси төркеме юк")
k("Proxies_EmptyHint", "请先在「订阅」页导入订阅配置", "Import a profile first", "請先在「訂閱」頁匯入訂閱設定", "まずサブスクをインポートしてください", "먼저 구독을 가져오세요", "Bitte zuerst ein Profil importieren", "Importa primero un perfil", "Сначала импортируйте подписку", "Önce profil içe aktarın", "Impor profil dulu", "ابتدا پروفایل را وارد کنید", "استورد ملفًا شخصيًا أولًا", "Беренче профильне кертегез")

# ---------- 订阅页 ----------
k("Profiles_Title", "订阅", "Profiles", "訂閱", "サブスクリプション", "구독", "Profile", "Perfiles", "Подписки", "Profiller", "Profil", "پروفایل‌ها", "الملفات الشخصية", "Профильләр")
k("Profiles_UrlPlaceholder", "输入订阅链接（https://...），支持 clash:// 安装链接", "Subscription URL (https://...), supports clash:// links", "輸入訂閱連結（https://...），支援 clash:// 安裝連結", "サブスクURLを入力（clash:// 対応）", "구독 URL 입력 (clash:// 지원)", "Abo-URL eingeben (clash:// wird unterstützt)", "URL de suscripción (admite clash://)", "URL подписки (поддерживается clash://)", "Abonelik URL'si (clash:// desteklenir)", "URL langganan (mendukung clash://)", "آدرس اشتراک (پشتیبانی از clash://)", "رابط الاشتراك (يدعم clash://)", "Абу URL (clash:// хуплана)")
k("Profiles_ImportLocal", "本地文件", "Local file", "本地檔案", "ローカルファイル", "로컬 파일", "Lokale Datei", "Archivo local", "Локальный файл", "Yerel dosya", "File lokal", "فایل محلی", "ملف محلي", "Локаль файл")
k("Profiles_CreateEmpty", "新建空配置", "New empty profile", "新建空設定", "空の設定を作成", "빈 설정 만들기", "Neues leeres Profil", "Nuevo perfil vacío", "Новый пустой профиль", "Yeni boş profil", "Profil kosong baru", "پروفایل خالی جدید", "ملف شخصي فارغ جديد", "Яңа буш профиль")
k("Profiles_UpdateAll", "全部更新", "Update all", "全部更新", "すべて更新", "모두 업데이트", "Alle aktualisieren", "Actualizar todo", "Обновить все", "Tümünü güncelle", "Perbarui semua", "همه را به‌روزرسانی کن", "تحديث الكل", "Барысын яңарту")
k("Profiles_GlobalEnhance", "全局增强", "Global enhancement", "全域增強", "グローバル拡張", "글로벌 향상", "Globale Erweiterung", "Mejora global", "Глобальные улучшения", "Küresel geliştirme", "Peningkatan global", "بهبود سراسری", "التحسين العام", "Глобаль яхшырту")
k("Profiles_Empty", "暂无订阅", "No profiles", "暫無訂閱", "サブスクなし", "구독 없음", "Keine Profile", "Sin perfiles", "Нет подписок", "Profil yok", "Tidak ada profil", "بدون پروفایل", "لا ملفات شخصية", "Профиль юк")
k("Profiles_EmptyHint", "粘贴订阅链接后点击「导入」，或选择本地 YAML 配置文件", "Paste a subscription URL and import, or pick a local YAML file", "貼上訂閱連結後點「匯入」，或選擇本地 YAML 設定檔", "URLを貼り付けてインポート、またはローカルYAMLを選択", "URL 붙여넣기 후 가져오기 또는 로컬 YAML 선택", "Abo-URL einfügen oder lokale YAML-Datei wählen", "Pega una URL e importa, o elige un YAML local", "Вставьте URL и импортируйте или выберите локальный YAML", "Abonelik URL'sini yapıştır ve içe aktar veya yerel YAML seç", "Tempel URL lalu impor, atau pilih file YAML lokal", "آدرس را بچسبانید و وارد کنید یا فایل محلی انتخاب کنید", "الصق رابط الاشتراك ثم استورد أو اختر ملف YAML محلي", "URL куйып кертегез яки җирле YAML сайлагыз")
k("Profiles_MenuSelect", "选择此订阅", "Use this profile", "選擇此訂閱", "このサブスクを使用", "이 구독 사용", "Dieses Profil verwenden", "Usar este perfil", "Использовать эту подписку", "Bu profili kullan", "Gunakan profil ini", "استفاده از این پروفایل", "استخدم هذا الملف", "Бу профильне куллану")
k("Profiles_MenuUpdate", "更新", "Update", "更新", "更新", "업데이트", "Aktualisieren", "Actualizar", "Обновить", "Güncelle", "Perbarui", "به‌روزرسانی", "تحديث", "Яңарту")
k("Profiles_MenuEdit", "编辑信息", "Edit info", "編輯資訊", "情報を編集", "정보 편집", "Info bearbeiten", "Editar info", "Изменить info", "Bilgiyi düzenle", "Edit info", "ویرایش اطلاعات", "تعديل المعلومات", "Мәгълүматны үзгәртү")
k("Profiles_MenuEnhance", "编辑增强配置", "Edit enhancement", "編輯增強設定", "拡張設定を編集", "향상 설정 편집", "Erweiterung bearbeiten", "Editar mejora", "Изменить улучшения", "Geliştirmeyi düzenle", "Edit peningkatan", "ویرایش بهبودها", "تعديل التحسينات", "Яхшыртуну үзгәртү")
k("Profiles_MenuUp", "上移", "Move up", "上移", "上へ", "위로", "Nach oben", "Subir", "Вверх", "Yukarı taşı", "Naik", "انتقال به بالا", "نقل لأعلى", "Өскә")
k("Profiles_MenuDown", "下移", "Move down", "下移", "下へ", "아래로", "Nach unten", "Bajar", "Вниз", "Aşağı taşı", "Turun", "انتقال به پایین", "نقل لأسفل", "Аска")
k("Profiles_MenuDelete", "删除", "Delete", "刪除", "削除", "삭제", "Löschen", "Eliminar", "Удалить", "Sil", "Hapus", "حذف", "حذف", "Бетерү")

# ---------- 连接页 ----------
k("Connections_Title", "连接", "Connections", "連接", "接続", "연결", "Verbindungen", "Conexiones", "Соединения", "Bağlantılar", "Koneksi", "اتصال‌ها", "الاتصالات", "Тоташулар")
k("Connections_SearchPlaceholder", "搜索主机 / 规则 / 进程", "Search host / rule / process", "搜尋主機 / 規則 / 進程", "ホスト/ルール/プロセス検索", "호스트/규칙/프로세스 검색", "Host/Regel/Prozess suchen", "Buscar host/regla/proceso", "Поиск: хост/правило/процесс", "Ana makine/kural/süreç ara", "Cari host/aturan/proses", "جستجوی میزبان/قانون/فرایند", "بحث عن المضيف/القاعدة/العملية", "Хост/кагыйдә/процесс эзләү")
k("Connections_CloseAll", "关闭全部", "Close all", "關閉全部", "すべて閉じる", "모두 닫기", "Alle schließen", "Cerrar todo", "Закрыть все", "Tümünü kapat", "Tutup semua", "بستن همه", "إغلاق الكل", "Барысын да ябу")
k("Connections_Closed", "已关闭", "Closed", "已關閉", "閉じ済み", "닫힘", "Geschlossen", "Cerradas", "Закрытые", "Kapalı", "Tertutup", "بسته‌شده", "مغلق", "Ябылган")
k("Connections_Host", "主机", "Host", "主機", "ホスト", "호스트", "Host", "Host", "Хост", "Ana makine", "Host", "میزبان", "المضيف", "Хост")
k("Connections_Network", "网络", "Network", "網路", "ネットワーク", "네트워크", "Netzwerk", "Red", "Сеть", "Ağ", "Jaringan", "شبکه", "الشبكة", "Челтәр")
k("Connections_Download", "下载", "Down", "下載", "ダウンロード", "다운로드", "Down", "Bajada", "Загрузка", "İndirme", "Unduh", "دانلود", "تنزيل", "Йөкләү")
k("Connections_Upload", "上传", "Up", "上傳", "アップロード", "업로드", "Up", "Subida", "Отдача", "Yükleme", "Unggah", "آپلود", "رفع", "Йөкләү")
k("Connections_Rule", "规则", "Rule", "規則", "ルール", "규칙", "Regel", "Regla", "Правило", "Kural", "Aturan", "قانون", "القاعدة", "Кагыйдә")
k("Connections_Chains", "链路", "Chains", "鏈路", "チェーン", "체인", "Kette", "Cadena", "Цепочка", "Zincir", "Rantai", "زنجیره", "السلسلة", "Чылбыр")
k("Connections_Process", "进程", "Process", "進程", "プロセス", "프로세스", "Prozess", "Proceso", "Процесс", "Süreç", "Proses", "فرایند", "العملية", "Процесс")
k("Connections_Time", "时间", "Time", "時間", "時刻", "시간", "Zeit", "Hora", "Время", "Zaman", "Waktu", "زمان", "الوقت", "Вакыт")
k("Connections_SortDefault", "默认排序", "Default order", "預設排序", "デフォルト順", "기본 정렬", "Standard", "Orden predeterminado", "По умолчанию", "Varsayılan", "Urutan default", "ترتیب پیش‌فرض", "الترتيب الافتراضي", "Тәртип буенча")
k("Connections_SortUpload", "按上传", "By upload", "按上傳", "アップロード順", "업로드순", "Nach Upload", "Por subida", "По отдаче", "Yüklemeye göre", "Berdasarkan unggah", "بر اساس آپلود", "حسب الرفع", "Йөкләү буенча")
k("Connections_SortDownload", "按下载", "By download", "按下載", "ダウンロード順", "다운로드순", "Nach Download", "Por bajada", "По загрузке", "İndirmeye göre", "Berdasarkan unduh", "بر اساس دانلود", "حسب التنزيل", "Йөкләү буенча")

# ---------- 规则页 ----------
k("Rules_Title", "规则", "Rules", "規則", "ルール", "규칙", "Regeln", "Reglas", "Правила", "Kurallar", "Aturan", "قوانین", "القواعد", "Кагыйдәләр")
k("Rules_SearchPlaceholder", "搜索规则内容", "Search rules", "搜尋規則內容", "ルールを検索", "규칙 검색", "Regeln suchen", "Buscar reglas", "Поиск правил", "Kural ara", "Cari aturan", "جستجوی قوانین", "بحث في القواعد", "Кагыйдә эзләү")
k("Rules_Providers", "规则 Provider", "Rule providers", "規則 Provider", "ルールプロバイダー", "규칙 공급자", "Regel-Provider", "Proveedores de reglas", "Поставщики правил", "Kural sağlayıcılar", "Penyedia aturan", "تأمین‌کنندگان قانون", "مزودات القواعد", "Кагыйдә тәэмин итүчеләр")
k("Rules_Empty", "暂无规则", "No rules", "暫無規則", "ルールなし", "규칙 없음", "Keine Regeln", "Sin reglas", "Нет правил", "Kural yok", "Tidak ada aturan", "بدون قانون", "لا قواعد", "Кагыйдә юк")

# ---------- 日志页 ----------
k("Logs_Title", "日志", "Logs", "日誌", "ログ", "로그", "Protokolle", "Registros", "Журналы", "Kayıtlar", "Log", "گزارش‌ها", "السجلات", "Журналлар")
k("Logs_LevelAll", "全部级别", "All levels", "全部級別", "全レベル", "모든 수준", "Alle Stufen", "Todos los niveles", "Все уровни", "Tüm düzeyler", "Semua level", "همه سطوح", "جميع المستويات", "Барлык дәрәҗә")
k("Logs_LevelInfo", "Info 及以上", "Info and above", "Info 及以上", "Info 以上", "Info 이상", "Info und höher", "Info y superior", "Info и выше", "Info ve üzeri", "Info ke atas", "Info و بالاتر", "معلومات وأعلى", "Info һәм югарырак")
k("Logs_LevelWarning", "Warning 及以上", "Warning and above", "Warning 及以上", "Warning 以上", "Warning 이상", "Warnung und höher", "Warning y superior", "Warning и выше", "Warning ve üzeri", "Warning ke atas", "Warning و بالاتر", "تحذير وأعلى", "Кисәтү һәм югарырак")
k("Logs_LevelError", "Error", "Error", "Error", "エラー", "오류", "Fehler", "Error", "Ошибки", "Hata", "Error", "خطا", "خطأ", "Хата")
k("Logs_LevelDebug", "Debug", "Debug", "Debug", "デバッグ", "디버그", "Debug", "Debug", "Отладка", "Hata ayıklama", "Debug", "اشکال‌زدایی", "تصحيح", "Дебуг")
k("Logs_SearchPlaceholder", "搜索日志内容", "Search logs", "搜尋日誌內容", "ログを検索", "로그 검색", "Protokolle durchsuchen", "Buscar registros", "Поиск по журналам", "Kayıtlarda ara", "Cari log", "جستجوی گزارش‌ها", "بحث في السجلات", "Журналларда эзләү")
k("Logs_Pause", "暂停", "Pause", "暫停", "一時停止", "일시정지", "Pause", "Pausa", "Пауза", "Duraklat", "Jeda", "توقف", "إيقاف مؤقت", "Туктау")
k("Logs_Clear", "清空", "Clear", "清空", "クリア", "지우기", "Leeren", "Limpiar", "Очистить", "Temizle", "Bersihkan", "پاک کردن", "مسح", "Чистарту")
k("Logs_NewestFirst", "倒序", "Newest first", "倒序", "新しい順", "최신순", "Neueste zuerst", "Más recientes", "Сначала новые", "En yeni önce", "Terbaru dulu", "جدیدترین اول", "الأحدث أولاً", "Яңалары башта")
k("Logs_Empty", "暂无日志", "No logs", "暫無日誌", "ログなし", "로그 없음", "Keine Protokolle", "Sin registros", "Нет журналов", "Kayıt yok", "Tidak ada log", "بدون گزارش", "لا سجلات", "Журнал юк")

# ---------- 解锁测试页 ----------
k("Unlock_Title", "解锁测试", "Unlock Test", "解鎖測試", "アンロックテスト", "잠금 해제 테스트", "Entsperrtest", "Prueba de desbloqueo", "Тест разблокировки", "Kilit Açma Testi", "Uji Buka Kunci", "تست رفع انسداد", "اختبار إلغاء القفل", "Ачуды тикшерү")
k("Unlock_Subtitle", "检测当前节点对流媒体与 AI 服务的解锁状态（请求经内核代理）", "Check streaming/AI service availability via the current node (through core proxy)", "檢測目前節點對串流與 AI 服務的解鎖狀態（請求經核心代理）", "現在のノードでストリーミング/AIサービスの利用可否を確認", "현재 노드의 스트리밍/AI 서비스 이용 가능 여부 확인", "Verfügbarkeit von Streaming-/KI-Diensten über den aktuellen Knoten prüfen", "Comprueba la disponibilidad de servicios de streaming/IA vía el nodo actual", "Проверка доступности стриминговых/AI-сервисов через текущий узел", "Akış/AI servislerinin kullanılabilirliğini mevcut düğümle kontrol et", "Periksa ketersediaan layanan streaming/AI via node saat ini", "بررسی دسترسی سرویس‌های استریم/AI از طریق گره فعلی", "تحقق من توفر خدمات البث/الذكاء الاصطناعي عبر العقدة الحالية", "Агым/AI хезмәтләренең ачылыгын тикшерү")
k("Unlock_RunAll", "全部测试", "Run all", "全部測試", "すべて実行", "모두 실행", "Alle testen", "Probar todo", "Запустить все", "Tümünü çalıştır", "Jalankan semua", "اجرای همه", "تشغيل الكل", "Барысын да эшләтү")
k("Unlock_Retest", "重测", "Retest", "重測", "再テスト", "다시 테스트", "Erneut testen", "Reprobar", "Повторить", "Yeniden test", "Uji ulang", "تست مجدد", "إعادة الاختبار", "Кабат тикшерү")

import i18n_settings_table
i18n_settings_table.register(k)

LANGS = ["zh-CN", "en-US", "zh-TW", "ja", "ko", "de", "es", "ru", "tr", "id", "fa", "ar", "tt"]

def esc(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;").replace('"', "&quot;")

def names_for(key):
    # 同一值同时提供 .Text 与 .Content（TextBlock 与 Button/ComboBoxItem 各取所需）；
    # 卡片另有 .Header/.Description。目标元素不支持的性质会被 MRT 忽略。
    if key.endswith(".Desc"):
        return [key[:-5] + ".Description"]
    if key.startswith("Settings_Card") or key.startswith("Settings_Section") or key == "Settings_Title":
        return [key + ".Header"]
    return [key + ".Text", key + ".Content"]

NL = chr(10)
def emit(lang, index):
    out = ['<?xml version="1.0" encoding="utf-8"?>', '<root>']
    for (key, *vals) in T:
        for name in names_for(key):
            out.append(f'  <data name="{name}" xml:space="preserve">')
            out.append(f'    <value>{esc(vals[index])}</value>')
            out.append('  </data>')
    out.append('</root>')
    path = os.path.join("Strings", lang, "Resources.resw")
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with io.open(path, "w", encoding="utf-8", newline=chr(10)) as f:
        f.write(NL.join(out) + NL)
    print(f"{path}: {len(T)}")

if __name__ == "__main__":
    for i, lang in enumerate(LANGS):
        emit(lang, i)
