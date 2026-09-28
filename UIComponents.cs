using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace KrutolFramework.Core
{
    /// <summary>
    /// Базовый класс для всех будущих элементов интерфейса
    /// </summary>
    public abstract class UIComponent
    {
        public Vector2 Position { get; set; }
        public Vector2 Size { get; set; }
        public bool IsVisible { get; set; } = true;
        public bool IsEnabled { get; set; } = true; // Активен ли элемент для кликов

        /// <summary>
        /// Проверяет, находится ли курсор мыши в границах прямоугольника элемента.
        /// </summary>
        public bool IsMouseOver()
        {
            if (!IsVisible || !IsEnabled) return false;
            Vector2 mouse = Input.VirtualMousePosition;
            return mouse.X >= Position.X && mouse.X <= Position.X + Size.X &&
                   mouse.Y >= Position.Y && mouse.Y <= Position.Y + Size.Y;
        }

        public abstract void Update(float deltaTime);
        public abstract void Render(SpriteBatch batch);
    }

    /// <summary>
    /// Невидимая кнопка для создания кликабельных зон поверх готовых артов фона
    /// </summary>
    public class UIInvisibleButton : UIComponent
    {
        public Action OnClick { get; set; }
        private bool _isPressed = false;

        public override void Update(float deltaTime)
        {
            if (!IsVisible || !IsEnabled) return;

            if (IsMouseOver())
            {
                if (Input.IsMouseButtonPressed(MouseButton.Left))
                {
                    _isPressed = true;
                }

                // Клик засчитывается, когда мышку отпустили ИМЕННО над кнопкой (как в PvZ)
                if (_isPressed && !Input.IsMouseButtonDown(MouseButton.Left))
                {
                    _isPressed = false;
                    OnClick?.Invoke();
                }
            }
            else
            {
                if (!Input.IsMouseButtonDown(MouseButton.Left))
                {
                    _isPressed = false;
                }
            }
        }

        public override void Render(SpriteBatch batch)
        {
            // Невидимая кнопка ничего не рисует
        }
    }

    /// <summary>
    /// Полноценная кнопка с поддержкой состояний (Замена текстур, Подсветка оверлеем и Нажатие)
    /// </summary>
    public class UIButton : UIComponent
    {
        // Основные текстурные состояния
        public TextureRegion TextureIdle { get; set; }       // Обычное состояние
        public TextureRegion TextureHover { get; set; }      // Состояние наведения (если null — используется замена цвета)
        public TextureRegion TexturePressed { get; set; }    // Состояние нажатия (если null — сдвигаем idle)
        public TextureRegion TextureDisabled { get; set; }   // Состояние выключенной кнопки

        // Дополнительная текстура для наложения «блеска» или рамки поверх базовой текстуры
        public TextureRegion TextureOverlay { get; set; }
        public bool UseOverlayOnHover { get; set; } = false; // Использовать Overlay как подсветку вместо полной замены?

        public Action OnClick { get; set; }

        protected Color4 CurrentColor { get; set; } = Color4.White;
        protected bool IsPressed { get; private set; } = false;

        public override void Update(float deltaTime)
        {
            if (!IsVisible || !IsEnabled) return;

            if (IsMouseOver())
            {
                // Эффект наведения цветом (только если не задана текстура Hover и не включен Overlay)
                if (TextureHover.AtlasTextureHandle == 0 && !UseOverlayOnHover)
                {
                    CurrentColor = new Color4(0.9f, 0.9f, 0.9f, 1.0f); // Чуть притемняем для интерактивности
                }
                else
                {
                    CurrentColor = Color4.White;
                }

                if (Input.IsMouseButtonPressed(MouseButton.Left))
                {
                    IsPressed = true;
                }

                if (IsPressed && !Input.IsMouseButtonDown(MouseButton.Left))
                {
                    IsPressed = false;
                    OnClick?.Invoke();
                }
            }
            else
            {
                CurrentColor = Color4.White;
                if (!Input.IsMouseButtonDown(MouseButton.Left))
                {
                    IsPressed = false;
                }
            }
        }

        public override void Render(SpriteBatch batch)
        {
            if (!IsVisible) return;

            // 1. Выбираем текстуру на основе текущего состояния
            TextureRegion activeTexture = TextureIdle;

            if (!IsEnabled)
            {
                if (TextureDisabled.AtlasTextureHandle != 0) activeTexture = TextureDisabled;
            }
            else if (IsPressed && IsMouseOver())
            {
                if (TexturePressed.AtlasTextureHandle != 0) activeTexture = TexturePressed;
            }
            else if (IsMouseOver())
            {
                if (TextureHover.AtlasTextureHandle != 0 && !UseOverlayOnHover) activeTexture = TextureHover;
            }

            if (activeTexture.AtlasTextureHandle == 0) return;

            // 2. Рассчитываем позицию и масштаб
            Vector2 drawPos = Position;
            Vector2 scale = new(Size.X / activeTexture.Width, Size.Y / activeTexture.Height);

            // PopCap фишка: если текстура нажатия не задана, мы просто сдвигаем базовый спрайт на 1-2 пикселя вбок и вниз
            if (IsPressed && IsMouseOver() && TexturePressed.AtlasTextureHandle == 0)
            {
                drawPos += new Vector2(1f, 1f);
            }

            // 3. Базовая отрисовка
            batch.Draw(activeTexture, drawPos, scale, 0f, CurrentColor);

            // 4. Отрисовка Оверлея (подсветка поверх)
            if (IsEnabled && IsMouseOver() && UseOverlayOnHover && TextureOverlay.AtlasTextureHandle != 0)
            {
                Vector2 overlayScale = new(Size.X / TextureOverlay.Width, Size.Y / TextureOverlay.Height);
                batch.Draw(TextureOverlay, drawPos, overlayScale, 0f, Color4.White, Anchor.Center);
            }
        }
    }

    public struct Button3PartSkin
    {
        public TextureRegion Left;
        public TextureRegion Middle; // Растягиваемая/тайлируемая центральная часть
        public TextureRegion Right;
    }

    /// <summary>
    /// Кнопка, расширяемая только по горизонтали (3-Part Slicing).
    /// Идеально для оригинальных кнопок меню, надписей "ОК", "ОТМЕНА" любой длины.
    /// </summary>
    public class UI3PartButton : UIComponent
    {
        public Button3PartSkin SkinIdle { get; set; }
        public Button3PartSkin SkinHover { get; set; }
        public Button3PartSkin SkinPressed { get; set; }

        public Action OnClick { get; set; }

        protected bool IsPressed = false;
        protected Color4 CurrentColor = Color4.White;

        public UI3PartButton(Button3PartSkin idleSkin, Vector2 position, float width)
        {
            SkinIdle = idleSkin;
            Position = position;

            // Высота кнопки жестко фиксируется по высоте исходного графического ассета краев
            float height = idleSkin.Left.Height;
            Size = new Vector2(width, height);
        }

        public override void Update(float deltaTime)
        {
            if (!IsVisible || !IsEnabled) return;

            if (IsMouseOver())
            {
                if (SkinHover.Left.AtlasTextureHandle == 0)
                {
                    CurrentColor = new Color4(0.85f, 0.85f, 0.85f, 1.0f); // Подсветка тоном
                }

                if (Input.IsMouseButtonPressed(MouseButton.Left)) IsPressed = true;

                if (IsPressed && !Input.IsMouseButtonDown(MouseButton.Left))
                {
                    IsPressed = false;
                    OnClick?.Invoke();
                }
            }
            else
            {
                CurrentColor = Color4.White;
                if (!Input.IsMouseButtonDown(MouseButton.Left)) IsPressed = false;
            }
        }

        public override void Render(SpriteBatch batch)
        {
            if (!IsVisible) return;

            // 1. Выбираем активный скин
            Button3PartSkin skin = SkinIdle;
            if (IsPressed && IsMouseOver() && SkinPressed.Left.AtlasTextureHandle != 0) skin = SkinPressed;
            else if (IsMouseOver() && SkinHover.Left.AtlasTextureHandle != 0) skin = SkinHover;

            if (skin.Left.AtlasTextureHandle == 0 || skin.Middle.AtlasTextureHandle == 0 || skin.Right.AtlasTextureHandle == 0) return;

            float x = Position.X;
            float y = Position.Y;
            float w = Size.X;

            float lw = skin.Left.Width;
            float rw = skin.Right.Width;
            float mw = skin.Middle.Width > 0 ? skin.Middle.Width : 1f;

            // Фиксация сдвига PopCap при нажатии без кастомного скина
            if (IsPressed && IsMouseOver() && SkinPressed.Left.AtlasTextureHandle == 0)
            {
                x += 1f;
                y += 1f;
            }

            // 2. Рисуем левый и правый неизменяемые края (Масштаб 1:1)
            batch.Draw(skin.Left, new Vector2(x, y), Vector2.One, 0f, CurrentColor);
            batch.Draw(skin.Right, new Vector2(x + w - rw - 10, y), Vector2.One, 0f, CurrentColor);

            // 3. Горизонтальный тайлинг (повторение) центральной части
            for (float drawX = x + lw; drawX < x + w - rw; drawX += mw)
            {
                float remainW = (x + w - rw) - drawX;
                float currentW = Math.Min(mw, remainW);
                float scaleX = currentW / mw;

                // Клонируем структуру UV, чтобы аккуратно обрезать текстуру на стыке правого края
                TextureRegion midSegment = skin.Middle;
                midSegment.U2 = midSegment.U1 + (midSegment.U2 - midSegment.U1) * scaleX; 
                midSegment.Width = (int)currentW; 
                batch.Draw(midSegment, new Vector2(drawX, y), new Vector2(scaleX, 1f), 0f, CurrentColor);
            }
        }
    }

    public class UITextInput : UIComponent
    {
        public string Text { get; set; } = "";
        public int MaxLength { get; set; } = 15;
        public bool IsFocused { get; set; } = false;

        public FontRenderer Font { get; set; }
        public TextureRegion BackgroundTexture { get; set; } // Текстура поля ввода (если есть)

        private float _blinkTimer = 0f;
        private bool _caretVisible = true;

        public override void Update(float deltaTime)
        {
            if (!IsVisible || !IsEnabled) return;

            // Клик для фокуса
            if (Input.IsMouseButtonPressed(MouseButton.Left))
            {
                IsFocused = IsMouseOver();
            }

            if (!IsFocused) return;

            // Мигание каретки
            _blinkTimer += deltaTime;
            if (_blinkTimer >= 0.5f)
            {
                _caretVisible = !_caretVisible;
                _blinkTimer = 0f;
            }

            // Классический Backspace
            if (Input.IsKeyDown(Keys.Backspace) && Text.Length > 0)
            {
                Text = Text.Substring(0, Text.Length - 1);
            }

            List<char> pressedChars = Input.GetFrameTextInput();
            if (pressedChars.Count > 0)
            {
                foreach (char c in pressedChars)
                {
                    if (Text.Length < MaxLength)
                    {
                        Text += c;
                    }
                }
                _blinkTimer = 0f;
                _caretVisible = true;
            }

        }

        public override void Render(SpriteBatch batch)
        {
            if (!IsVisible) return;

            // 1. Отрисовка подложки/рамки
            if (BackgroundTexture.AtlasTextureHandle != 0)
            {
                Vector2 scale = new(Size.X / BackgroundTexture.Width, Size.Y / BackgroundTexture.Height);
                batch.Draw(BackgroundTexture, Position, scale, 0f, Color4.White);
            }

            if (Font == null) return;

            // 2. Отрисовка текста внутри поля ввода (по центру вертикали)
            Vector2 textScale = Vector2.One;
            Vector2 textSize = Font.MeasureString(Text, textScale);
            Vector2 textPos = new(Position.X + 15f, Position.Y + (Size.Y - textSize.Y) * 0.5f);

            Font.DrawText(batch, Text, textPos, textScale, Color4.Black, TextAlignment.Left);

            // 3. Отрисовка вертикальной каретки
            if (IsFocused && _caretVisible)
            {
                float caretX = textPos.X + textSize.X + 2f;
                // Запасной глиф '|' или ручной прямоугольник через SpriteBatch
                Font.DrawText(batch, "|", new Vector2(caretX, textPos.Y), textScale, Color4.Black, TextAlignment.Left);
            }
        }
    }
    /// <summary>
    /// Компонент переключателя (Чекбокс)
    /// </summary>
    public class UICheckbox : UIComponent
    {
        // Две текстуры для разных состояний
        public TextureRegion CheckedTexture { get; set; }
        public TextureRegion UncheckedTexture { get; set; }

        // Текущее состояние
        public bool IsChecked { get; set; }

        // Событие, вызываемое при изменении состояния
        public Action<bool> OnCheckedChanged { get; set; }
        public new bool IsMouseOver()
        {
            Vector2 mouse = Input.MousePosition;
            return mouse.X >= Position.X && mouse.X <= Position.X + Size.X &&
                   mouse.Y >= Position.Y - Size.Y / 2 && mouse.Y <= Position.Y + Size.Y / 2;
        }

        public override void Update(float deltaTime)
        {
            if (!IsVisible) return;

            // Если кликнули по чекбоксу — инвертируем его состояние
            if (IsMouseOver() && Input.IsMouseButtonPressed(MouseButton.Left))
            {
                IsChecked = !IsChecked;
                OnCheckedChanged?.Invoke(IsChecked);
            }
        }

        public override void Render(SpriteBatch batch)
        {
            if (!IsVisible) return;

            // Выбираем нужную текстуру в зависимости от состояния
            TextureRegion currentTexture = IsChecked ? CheckedTexture : UncheckedTexture;
            if (currentTexture.AtlasTextureHandle == 0) return;

            Vector2 scale = new(Size.X / currentTexture.Width, Size.Y / currentTexture.Height);
            batch.Draw(currentTexture, Position, scale, 0f, Color4.White);
        }
    }

    /// <summary>
    /// Компонент ползунка (Слайдер)
    /// </summary>
    public class UISlider : UIComponent
    {
        public TextureRegion BackgroundTexture { get; set; }
        public TextureRegion HandleTexture { get; set; } // Текстура самого ползунка

        // Значение слайдера от 0.0f до 1.0f
        public float Value { get; set; }

        // Внутреннее состояние: тащим ли мы ползунок прямо сейчас
        private bool _isDragging = false;
        public new bool IsMouseOver()
        {
            Vector2 mouse = Input.MousePosition;
            return mouse.X >= Position.X && mouse.X <= Position.X + Size.X &&
                   mouse.Y >= Position.Y - Size.Y / 2 && mouse.Y <= Position.Y + Size.Y / 2;
        }
        public override void Update(float deltaTime)
        {
            if (!IsVisible) return;

            // Если нажали левую кнопку мыши НАД слайдером — включаем режим перетаскивания
            if (IsMouseOver() && Input.IsMouseButtonPressed(MouseButton.Left))
            {
                _isDragging = true;
            }

            // Если кнопку мыши отпустили (в любом месте экрана) — выключаем режим перетаскивания
            if (!Input.IsMouseButtonDown(MouseButton.Left))
            {
                _isDragging = false;
            }

            // Если мы в режиме перетаскивания, считаем значение ползунка
            if (_isDragging)
            {
                // Вычисляем локальную координату X мыши относительно левой границы слайдера
                float localMouseX = Input.MousePosition.X - Position.X;

                // Переводим пиксели в диапазон от 0.0 до 1.0 и жестко ограничиваем рамками
                Value = Math.Clamp(localMouseX / Size.X, 0f, 1f);
            }
        }

        public override void Render(SpriteBatch batch)
        {
            if (!IsVisible) return;

            // 1. Отрисовка фона слайдера (растягиваем под размер компонента)
            if (BackgroundTexture.AtlasTextureHandle != 0)
            {
                Vector2 bgScale = new(Size.X / BackgroundTexture.Width, Size.Y / BackgroundTexture.Height);
                batch.Draw(BackgroundTexture, Position, bgScale, 0f, Color4.White);
            }

            // 2. Отрисовка ползунка (рисуем его 1 в 1 без растягивания, центрируя по вертикали)
            if (HandleTexture.AtlasTextureHandle != 0)
            {
                // Позиция X зависит от текущего Value (процента)
                float handleX = Position.X + (Value * Size.X) - (HandleTexture.Width * 0.5f);
                // Центрируем ползунок по высоте слайдера
                float handleY = Position.Y + (Size.Y * 0.8f) - (HandleTexture.Height * 0.5f);

                batch.Draw(HandleTexture, new Vector2(handleX, handleY), Vector2.One, 0f, Color4.White);
            }
        }
    }

    public struct DialogWindowSkin
        {
        public TextureRegion TopLeft;
        public TextureRegion TopMiddle;   // Верхняя рамка тела
        public TextureRegion TopRight;
        public TextureRegion CenterLeft;
        public TextureRegion CenterMiddle;
        public TextureRegion CenterRight;
        public TextureRegion BottomLeft;
        public TextureRegion BottomMiddle;
        public TextureRegion BottomRight;
    }

    /// <summary>
    /// Перемещаемое диалоговое окно с нерастягиваемым фиксированным хедером
    /// </summary>
    public class UIDialogWindow : UIComponent
    {
        public DialogWindowSkin Skin { get; set; }

        // Отдельная текстура заголовка, которая не деформируется
        public TextureRegion HeaderTexture { get; set; }

        public float CornerWidth => Skin.TopLeft.Width;
        public float CornerHeight => Skin.TopLeft.Height;

        // Физические размеры хедера берутся напрямую из его .png файла
        public float HeaderWidth => HeaderTexture.Width;
        public float HeaderHeight => HeaderTexture.Height;

        private bool _isDragging = false;
        private Vector2 _dragOffset;

        public UIDialogWindow(DialogWindowSkin skin, TextureRegion headerTexture, Vector2 position, Vector2 size)
        {
            Skin = skin;
            HeaderTexture = headerTexture;
            Position = position;
            Size = size;
        }

        public override void Update(float deltaTime)
        {
            if (!IsVisible) return;

            Vector2 mouse = Input.MousePosition;

            // КРИТИЧЕСКИЙ РАСЧЕТ: Проверяем наведение строго на физические границы нерастянутого хедера.
            // Хедер центрирован по верхней кромке окна или прижат к левому краю? 
            // Сделаем классический вариант: хедер прижат к левому верхнему углу окна.
            bool isOverHeader = mouse.X >= Position.X + Size.X/2 && mouse.X <= Position.X + Size.X / 2 + HeaderWidth &&
                               mouse.Y >= Position.Y && mouse.Y <= Position.Y + HeaderHeight;

            if (isOverHeader && Input.IsMouseButtonPressed(MouseButton.Left) && !_isDragging)
            {
                _isDragging = true;
                _dragOffset = mouse - Position;
            }

            if (!Input.IsMouseButtonDown(MouseButton.Left))
            {
                _isDragging = false;
            }

            if (_isDragging)
            {
                Vector2 newPos = mouse - _dragOffset;

                // Защита от выхода за границы экрана 1280x720
                float maxX = 1600f - Size.X;
                float maxY = 900f - Size.Y;

                newPos.X = Math.Clamp(newPos.X, 0f, maxX);
                newPos.Y = Math.Clamp(newPos.Y, 0f, maxY);

                Position = newPos;
            }
        }

        public override void Render(SpriteBatch batch)
        {
            if (!IsVisible) return;

            float x = Position.X;
            float y = Position.Y;
            float w = Size.X;
            float h = Size.Y;

            float cw = CornerWidth;
            float ch = CornerHeight;

            float bodyY = y + HeaderHeight;
            float bodyH = h - HeaderHeight;


            // === 2. ОТРИСОВКА УГЛОВ ТЕЛА (Масштаб 1:1) ===
            batch.Draw(Skin.TopLeft, new Vector2(x, bodyY), Vector2.One, 0f, Color4.White);
            batch.Draw(Skin.TopRight, new Vector2(x + w - cw, bodyY), Vector2.One, 0f, Color4.White);
            batch.Draw(Skin.BottomLeft, new Vector2(x, bodyY + bodyH - ch), Vector2.One, 0f, Color4.White);
            batch.Draw(Skin.BottomRight, new Vector2(x + w - cw, bodyY + bodyH - ch), Vector2.One, 0f, Color4.White);

            // === 3. ТАЙЛИНГ ГОРИЗОНТАЛЬНЫХ И ВЕРТИКАЛЬНЫХ КРАЕВ ===
            float tmW = Skin.TopMiddle.Width > 0 ? Skin.TopMiddle.Width : 1f;
            for (float drawX = x + cw; drawX < x + w - cw; drawX += tmW)
            {
                float remainW = (x + w - cw) - drawX;
                float currentW = Math.Min(tmW, remainW);
                float scaleX = currentW / tmW;

                TextureRegion repeatTop = Skin.TopMiddle;
                repeatTop.U2 = repeatTop.U1 + (repeatTop.U2 - repeatTop.U1) * scaleX;
                batch.Draw(repeatTop, new Vector2(drawX, bodyY), new Vector2(scaleX, 1f), 0f, Color4.White);

                TextureRegion repeatBot = Skin.BottomMiddle;
                repeatBot.U2 = repeatBot.U1 + (repeatBot.U2 - repeatBot.U1) * scaleX;
                batch.Draw(repeatBot, new Vector2(drawX, bodyY + bodyH - ch), new Vector2(scaleX, 1f), 0f, Color4.White);
            }

            float clH = Skin.CenterLeft.Height > 0 ? Skin.CenterLeft.Height : 1f;
            for (float drawY = bodyY + ch; drawY < bodyY + bodyH - ch; drawY += clH)
            {
                float remainH = (bodyY + bodyH - ch) - drawY;
                float currentH = Math.Min(clH, remainH);
                float scaleY = currentH / clH;

                TextureRegion repeatLeft = Skin.CenterLeft;
                repeatLeft.V2 = repeatLeft.V1 + (repeatLeft.V2 - repeatLeft.V1) * scaleY;
                batch.Draw(repeatLeft, new Vector2(x, drawY), new Vector2(1f, scaleY), 0f, Color4.White);

                TextureRegion repeatRight = Skin.CenterRight;
                repeatRight.V2 = repeatRight.V1 + (repeatRight.V2 - repeatRight.V1) * scaleY;
                batch.Draw(repeatRight, new Vector2(x + w - cw, drawY), new Vector2(1f, scaleY), 0f, Color4.White);
        }

            // === 4. ТАЙЛИНГ ЦЕНТРА ===
            float cmW = Skin.CenterMiddle.Width > 0 ? Skin.CenterMiddle.Width : 1f;
            float cmH = Skin.CenterMiddle.Height > 0 ? Skin.CenterMiddle.Height : 1f;

            for (float drawY = bodyY + ch; drawY < bodyY + bodyH - ch; drawY += cmH)
            {
                float remainH = (bodyY + bodyH - ch) - drawY;
                float currentH = Math.Min(cmH, remainH);
                float scaleY = currentH / cmH;

                for (float drawX = x + cw; drawX < x + w - cw; drawX += cmW)
                {
                    float remainW = (x + w - cw) - drawX;
                    float currentW = Math.Min(cmW, remainW);
                    float scaleX = currentW / cmW;

                    TextureRegion repeatCenter = Skin.CenterMiddle;
                    repeatCenter.U2 = repeatCenter.U1 + (repeatCenter.U2 - repeatCenter.U1) * scaleX;
                    repeatCenter.V2 = repeatCenter.V1 + (repeatCenter.V2 - repeatCenter.V1) * scaleY;

                    batch.Draw(repeatCenter, new Vector2(drawX, drawY), new Vector2(scaleX, scaleY), 0f, Color4.White);
                }
            }
            if (HeaderTexture.AtlasTextureHandle != 0)
            {
                batch.Draw(HeaderTexture, new Vector2(x + w / 2f, y + HeaderHeight - 10f), Vector2.One, 0f, Color4.White, Anchor.Center);
            }
        }
    }
}
