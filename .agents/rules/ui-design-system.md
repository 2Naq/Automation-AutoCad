# UI Design System & Styling Rules - CadElectricalToolkit

> **Scope:** WPF Views, Controls, Dialogs, and Styles in `src/CadElectricalToolkit/UI/` and `src/CadElectricalToolkit.UiRunner/`.  
> **Target Theme:** Modern Dark Theme with Tech Blue Accent (`#2563EB` / `#3B82F6` / `#38BDF8`).

---

## 1. Color Palette Tokens

All WPF dialogs must strictly adhere to the following color palette. Never introduce random colors (e.g. purple, orange, unaligned green/red) outside these tokens.

| Role | Color Hex | Usage |
| :--- | :--- | :--- |
| **Window Background** | `#0F1117` | Root background for all Windows |
| **Header Bar** | `#141722` | Header background (`BorderThickness="0,0,0,1"`, `BorderBrush="#1F2433"`) |
| **Footer Bar** | `#111318` | Footer bar background (`BorderThickness="0,1,0,0"`, `BorderBrush="#1F2433"`) |
| **Card / Group Panel** | `#181B26` | Card background (`BorderBrush="#2B3245"`, `CornerRadius="8"`) |
| **Input Background** | `#12151E` | TextBox, ComboBox background (`BorderBrush="#2B3245"`, `CornerRadius="5"`) |
| **Input Focus** | `#161825` | Background on `IsFocused="True"` (`BorderBrush="#3B82F6"`) |
| **Primary Button** | `#2563EB` | Primary CTA background. Hover: `#3B82F6`, Pressed: `#1D4ED8` |
| **Secondary Button** | `#1E2230` | Secondary button background (`BorderBrush="#2E354A"`). Hover: `#282F42`, Border: `#3B82F6` |
| **Preview Panel** | `#0B1A2F` | Live preview container background (`BorderBrush="#1D4ED8"`, `CornerRadius="6"`) |
| **Preview Label** | `#93C5FD` | "Xem trước: " label text color (`FontWeight="SemiBold"`) |
| **Tech Blue Accent** | `#38BDF8` | Live preview value, target block/tag names, CaretBrush, active tab text |
| **Success Status** | `#4ADE80` | Status text for successful operations |
| **Error / Alert** | `#F87171` | Strictly reserved for error messages or destructive actions |
| **Field Labels** | `#94A3B8` | Field label text (`FontWeight="SemiBold"`, `FontSize="11.5"`) |
| **Muted Text** | `#64748B` | Footnotes, hints, and disabled indicators |
| **Scrollbar Thumb** | `#333C52` | Scrollbar thumb. Hover: `#64748B`, Dragging: `#38BDF8` |

---

## 2. Reusable XAML Styles

Every window must include or reuse these standard styles in its `<Window.Resources>`:

### 2.1. PreviewPanel
```xaml
<Style x:Key="PreviewPanel" TargetType="Border">
    <Setter Property="Background" Value="#0B1A2F"/>
    <Setter Property="BorderBrush" Value="#1D4ED8"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="CornerRadius" Value="6"/>
    <Setter Property="Padding" Value="10,6"/>
    <Setter Property="Margin" Value="0,4,0,0"/>
</Style>
```
**Application:**
```xaml
<Border Style="{StaticResource PreviewPanel}">
    <DockPanel>
        <TextBlock Text="Xem trước: " FontWeight="SemiBold" Foreground="#93C5FD" VerticalAlignment="Center"/>
        <TextBlock Text="{Binding PreviewText}" FontWeight="Bold" FontSize="13" 
                   Foreground="#38BDF8" VerticalAlignment="Center" TextTrimming="CharacterEllipsis"/>
    </DockPanel>
</Border>
```

### 2.2. PrimaryButton
```xaml
<Style x:Key="PrimaryButton" TargetType="Button">
    <Setter Property="Background" Value="#2563EB"/>
    <Setter Property="Foreground" Value="#FFFFFF"/>
    <Setter Property="FontWeight" Value="Bold"/>
    <Setter Property="FontSize" Value="12"/>
    <Setter Property="Height" Value="36"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="BorderThickness" Value="0"/>
    <Setter Property="Padding" Value="14,0"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="border" Background="{TemplateBinding Background}" CornerRadius="6">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="border" Property="Background" Value="#3B82F6"/>
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter TargetName="border" Property="Background" Value="#1D4ED8"/>
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter TargetName="border" Property="Opacity" Value="0.5"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

### 2.3. SecondaryButton
```xaml
<Style x:Key="SecondaryButton" TargetType="Button">
    <Setter Property="Background" Value="#1E2230"/>
    <Setter Property="Foreground" Value="#E2E8F0"/>
    <Setter Property="FontWeight" Value="SemiBold"/>
    <Setter Property="FontSize" Value="11.5"/>
    <Setter Property="Height" Value="29"/>
    <Setter Property="Cursor" Value="Hand"/>
    <Setter Property="BorderBrush" Value="#2E354A"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="Padding" Value="10,0"/>
    <Setter Property="Template">
        <Setter.Value>
            <ControlTemplate TargetType="Button">
                <Border x:Name="border" Background="{TemplateBinding Background}" 
                        BorderBrush="{TemplateBinding BorderBrush}" 
                        BorderThickness="{TemplateBinding BorderThickness}" CornerRadius="5">
                    <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                </Border>
                <ControlTemplate.Triggers>
                    <Trigger Property="IsMouseOver" Value="True">
                        <Setter TargetName="border" Property="Background" Value="#282F42"/>
                        <Setter TargetName="border" Property="BorderBrush" Value="#3B82F6"/>
                    </Trigger>
                    <Trigger Property="IsPressed" Value="True">
                        <Setter TargetName="border" Property="Background" Value="#161B28"/>
                    </Trigger>
                    <Trigger Property="IsEnabled" Value="False">
                        <Setter TargetName="border" Property="Opacity" Value="0.4"/>
                    </Trigger>
                </ControlTemplate.Triggers>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>
```

---

## 3. Strict Rules & Constraints

1. **NO Hardcoded Rogue Colors:** Do not use legacy purple (`#4F46E5`, `#7C3AED`, `#818CF8`) or legacy green (`#0D2818`) for buttons or preview cards.
2. **NO Red on Normal Fields:** Do NOT color normal block names or tag textboxes `#F87171` (red). Use `#38BDF8` for highlighted CAD identifiers and default `#F1F5F9` for normal text. Red is strictly reserved for validation errors.
3. **Pure MVVM Compliance:** Never put AutoCAD database operations or user prompts directly in `.xaml.cs` code-behind. Use `ViewModelBase` and `ICadInteractionService`.
4. **Visual Consistency:** Always use `FontFamily="Segoe UI"`, `UseLayoutRounding="True"`, and proper padding/margins.
