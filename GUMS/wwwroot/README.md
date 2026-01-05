# Static Files Directory

This directory contains static assets served by the application.

## Unit Logo

The file `UnitLogo.svg` is a placeholder logo displaying a simple trefoil design.

### Customizing the Logo

To use your own unit logo:

1. Replace `UnitLogo.svg` with your unit's official logo file
2. Keep the filename as `UnitLogo.svg` (or update the reference in `Components/Layout/NavMenu.razor`)
3. Recommended dimensions: 120x120 pixels or similar square aspect ratio
4. Supported formats: SVG (preferred), PNG, or JPG

### Girl Guiding Branding Guidelines

Please ensure your logo follows the [Girl Guiding Brand Guidelines](https://girlguiding.foleon.com/girlguiding-brand-guidelines/brand-guidelines/):

- Use official Girl Guiding logos and colors
- Maintain appropriate clear space around the logo
- Ensure good contrast against the dark blue sidebar background
- Logo should be clearly visible and not distorted

### Logo Specifications

Current logo styling (defined in `Components/Layout/NavMenu.razor.css`):
- Height: 70px
- Width: Auto (maintains aspect ratio)
- Margin right: 15px
- Position: Top of sidebar navigation

If you need different sizing, modify the `.unit-logo` class in the NavMenu CSS file.
