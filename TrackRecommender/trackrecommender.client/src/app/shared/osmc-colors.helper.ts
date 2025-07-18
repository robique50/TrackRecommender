export class OsmcColorsHelper {
  private static readonly OSMC_COLORS: { [key: string]: string } = {
    red: '#FF0000',
    blue: '#0000FF',
    yellow: '#FFFF00',
    green: '#008000',
    white: '#FFFFFF',
    black: '#000000',
    orange: '#FFA500',
    purple: '#800080',
    brown: '#A52A2A',
    gray: '#808080',
    grey: '#808080',
    none: 'transparent',
  };

  private static readonly COLOR_NAMES: { [key: string]: string } = {
    '#FF0000': 'Red',
    '#0000FF': 'Blue',
    '#FFFF00': 'Yellow',
    '#008000': 'Green',
    '#FFFFFF': 'White',
    '#000000': 'Black',
    '#FFA500': 'Orange',
    '#800080': 'Purple',
    '#A52A2A': 'Brown',
    '#808080': 'Gray',
    transparent: 'Transparent',
  };

  private static readonly SHAPE_NAMES: { [key: string]: string } = {
    stripe: 'Stripe',
    dot: 'Dot',
    circle: 'Circle',
    cross: 'Cross',
    triangle: 'Triangle',
    bar: 'Bar',
    frame: 'Frame',
    diamond: 'Diamond',
    fork: 'Fork',
    corner: 'Corner',
    backslash: 'Backslash',
    slash: 'Slash',
  };

  static getColorHex(colorName: string): string {
    const lowerName = colorName.toLowerCase();
    return this.OSMC_COLORS[lowerName] || colorName;
  }

  static getColorNameRo(colorHex: string): string {
    const upperHex = colorHex.toUpperCase();
    return this.COLOR_NAMES[upperHex] || colorHex;
  }

  static getShapeNameRo(shape: string): string {
    const lowerShape = shape.toLowerCase();
    return this.SHAPE_NAMES[lowerShape] || shape;
  }

  static needsBorder(backgroundColor: string): boolean {
    const lightColors = ['#FFFFFF', '#FFFF00', 'transparent'];
    return lightColors.includes(backgroundColor.toUpperCase());
  }

  static getContrastColor(backgroundColor: string): string {
    const darkBackgrounds = [
      '#000000',
      '#0000FF',
      '#008000',
      '#800080',
      '#A52A2A',
    ];
    if (darkBackgrounds.includes(backgroundColor.toUpperCase())) {
      return '#FFFFFF';
    }
    return '#000000';
  }

  static getMarkingStyle(marking: any): any {
    const style: any = {
      backgroundColor: marking.backgroundColor || '#FFFFFF',
    };

    if (this.needsBorder(marking.backgroundColor)) {
      style.border = '2px solid rgba(0, 0, 0, 0.2)';
    }

    return style;
  }

  static formatMarkingName(marking: any): string {
    const shapeName = this.getShapeNameRo(marking.shape);
    const colorName = marking.shapeColor
      ? this.getColorNameRo(marking.shapeColor)
      : this.getColorNameRo(marking.foregroundColor);

    return `${shapeName} ${colorName}`;
  }
}
