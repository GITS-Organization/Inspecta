import { fillRatio, generateSerials, joinedLength, parseSerials, splitSerials, suggestedCertificateCount } from './serials';

describe('serials', () => {
  it('generates padded serials and handles zero and negative quantities', () => {
    expect(generateSerials('CS-', 3)).toEqual(['CS-0001', 'CS-0002', 'CS-0003']);
    expect(generateSerials('CS-', 0)).toEqual([]);
    expect(generateSerials('CS-', -4)).toEqual([]);
    expect(generateSerials('CS-', 99999).length).toBe(5000);
  });

  it('parses lines, ignores blanks, and drops repeats case-insensitively', () => {
    const r = parseSerials('A1\n\n  A2 \na1\nA3, A4;A5');
    expect(r.serials).toEqual(['A1', 'A2', 'A3', 'A4', 'A5']);
    expect(r.duplicates).toBe(1);
  });

  it('flags serials longer than the limit', () => {
    expect(parseSerials('x'.repeat(61) + '\nok').tooLong).toBe(1);
  });

  it('expands a range, and keeps a backwards one as plain text', () => {
    expect(parseSerials('CS-26-0001 to CS-26-0004').serials).toEqual(['CS-26-0001', 'CS-26-0002', 'CS-26-0003', 'CS-26-0004']);
    expect(parseSerials('A-0005 to A-0001').serials).toEqual(['A-0005 to A-0001']);
  });

  it('splits evenly, in order, never leaving a certificate empty', () => {
    const s = generateSerials('S', 10);
    const parts = splitSerials(s, 3);
    expect(parts.map((p) => p.length)).toEqual([4, 3, 3]);
    expect(parts.flat()).toEqual(s);
    expect(splitSerials(s, 50).length).toBe(10);
    expect(splitSerials(s, 0).length).toBe(1);
    expect(splitSerials([], 3)).toEqual([[]]);
  });

  it('counts the ", " separators in the certificate length', () => {
    expect(joinedLength(['ab', 'cd', 'ef'])).toBe(10);
    expect(joinedLength([])).toBe(0);
  });

  it('suggests the fewest certificates that fit 600 characters', () => {
    const s = generateSerials('CS-26-', 1000); // 10 characters each
    const count = suggestedCertificateCount(s);
    expect(fillRatio(splitSerials(s, count))).toBeLessThanOrEqual(1);
    expect(fillRatio(splitSerials(s, count - 1))).toBeGreaterThan(1);
    expect(suggestedCertificateCount(['a'])).toBe(1);
  });
});
