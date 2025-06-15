export enum ShapeType {
  Svg = 0,
  Emoji = 1,
  Text = 2,
}

export interface TrailMarking {
  symbol: string;
  foregroundColor: string;
  backgroundColor: string;
  shape: string;
  shapeColor?: string;
  shapeType: ShapeType;
  displayName: string;
}

export const MARKING_COLORS = {
  red: '#FF0000',
  blue: '#0000FF',
  yellow: '#FFFF00',
  green: '#008000',
  white: '#FFFFFF',
  black: '#000000',
  orange: '#FFA500',
  purple: '#800080',
  brown: '#A52A2A',
} as const;

export const MARKING_SHAPES = {
  stripe: 'Stripe',
  dot: 'Dot',
  circle: 'Circle',
  cross: 'Cross',
  triangle: 'Triangle',
  bar: 'Bar',
  frame: 'Frame',
} as const;

export type MarkingColor = keyof typeof MARKING_COLORS;
export type MarkingShape = keyof typeof MARKING_SHAPES;
