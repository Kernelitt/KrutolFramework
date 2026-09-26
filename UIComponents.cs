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

        /// <summary>
        /// Проверяет, находится ли курсор мыши в границах прямоугольника элемента.
        /// Использует чистые координаты окна.
        /// </summary>
        public bool IsMouseOver()
        {
            Vector2 mouse = Input.MousePosition;
            return mouse.X >= Position.X && mouse.X <= Position.X + Size.X &&
                   mouse.Y >= Position.Y && mouse.Y <= Position.Y + Size.Y;
        }

        public abstract void Update(float deltaTime);
        public abstract void Render(SpriteBatch batch);
    }

    /// <summary>
    /// Простая кнопка с геометрической проверкой клика
    /// </summary>
    public class UIButton : UIComponent
    {
        // Текстура кнопки из нашего атласа
        public TextureRegion Texture { get; set; }

        // Событие, которое выполнится при клике
        public Action OnClick { get; set; }

        // Цвет, в который будет окрашиваться кнопка (например, для эффекта наведения)
        protected Color4 CurrentColor { get; set; } = Color4.White;

        public override void Update(float deltaTime)
        {
            if (!IsVisible) return;

            // Если мышь над кнопкой — подсвечиваем её (например, делаем чуть темнее или светлее)
            if (IsMouseOver())
            {
                CurrentColor = new Color4(0.8f, 0.8f, 0.8f, 1.0f); // Серый оттенок при наведении

                // Используем наш рабочий триггер одиночного клика из класса Input
                if (Input.IsMouseButtonPressed(MouseButton.Left))
                {
                    OnClick?.Invoke();
                }
            }
            else
            {
                CurrentColor = Color4.White; // Обычный цвет, если мышь далеко
            }
        }

        public override void Render(SpriteBatch batch)
        {
            if (!IsVisible || Texture.AtlasTextureHandle == 0) return;

            // Вычисляем масштаб спрайта, чтобы он растянулся ровно под физический размер кнопки
            Vector2 scale = new(Size.X / Texture.Width, Size.Y / Texture.Height);

            // Отрисовываем кнопку через наш высокопроизводительный SpriteBatch
            batch.Draw(Texture, Position, scale, 0f, CurrentColor);
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
            bool isOverHeader = mouse.X >= Position.X + CornerWidth * 2 && mouse.X <= Position.X + CornerWidth * 2 + HeaderWidth &&
                               mouse.Y >= Position.Y - 15 && mouse.Y <= Position.Y + HeaderHeight - 15;

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
                batch.Draw(HeaderTexture, new Vector2(x + cw * 2f, y), Vector2.One, 0f, Color4.White);
            }
        }
    }
}
