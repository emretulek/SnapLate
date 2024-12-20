# SnapLate Widget Plugin

This project was developed as a screean translater widget for windows widgets. With this widget, you can use translate on your desktop, you can extract and translate the text and images from the text and images that cannot be copied.

Tesseract needs to know the language of the text to be scanned in advance, so before scanning any text you must select the same language as the text to be scanned. 
For example; If you are going to scan a text in Russian, scanning will fail when English is selected, make sure you select Russian first.

[Tesseract](https://github.com/tesseract-ocr/tesseract) library was used to convert the screenshot to text. Google translate API is used for the translate API.

### Internal Settings

You can assign a different shortcut by typing `ALT`, `SHIFT` and `CTRL` `+` `letter` for combinations with the modified keys. Default shotcut is `ALT+S`

```json
{
  "short_cut": "ALT+S",
  "source_lang": "fr",
  "target_lang": "en",
  "font_size": 14.0,
  "primary_color": "#FFA52A2A",
  "primary_color_light": "#FFBC8F8F",
  "secondary_color": "#FF999999",
  "secondary_color_light": "#FF212121",
  "text_color": "#FFBBBBBB"
}
```

### Screenshots

![SnapLate](https://raw.githubusercontent.com/emretulek/SnapLate/refs/heads/master/sc_snaplate/snaplate_1.jpg)


![SnapLate_screenshot](https://raw.githubusercontent.com/emretulek/SnapLate/refs/heads/master/sc_snaplate/snaplate_2.jpg)
