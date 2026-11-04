import { CompanyFormValue, buildCreateRequest, readPngAsBase64, splitList, validateCompanyForm } from './company-request';

const base: CompanyFormValue = {
  fullName: ' Karim Samy ', email: ' karim@delta.test ', password: 'a-long-password-1', subscriptionMonths: 12,
  useBuiltInDetails: false, name: ' Delta Inspection ', address: ' 5 Industrial Zone, Cairo ', certificateTitle: '', issuerName: '',
  tagline: '', website: '', signerName: '', phones: '', emails: '', primaryColor: '#7a1f2b', accentColor: '#e0a100',
  logoBase64: null, accreditationsBase64: null,
};

describe('buildCreateRequest', () => {
  it('sends name and address together with the colors, and leaves empty optional fields out', () => {
    expect(buildCreateRequest(base)).toEqual({
      email: 'karim@delta.test', fullName: 'Karim Samy', password: 'a-long-password-1', subscriptionMonths: 12,
      companyProfile: { name: 'Delta Inspection', address: '5 Industrial Zone, Cairo', primaryColor: '#7a1f2b', accentColor: '#e0a100' },
    });
  });

  it('with built-in details sends neither name nor address, only images and colors', () => {
    const request = buildCreateRequest({ ...base, useBuiltInDetails: true, tagline: 'ignored', phones: '1', logoBase64: 'AAAA' });
    expect(request.companyProfile).toEqual({ primaryColor: '#7a1f2b', accentColor: '#e0a100', logoBase64: 'AAAA' });
  });

  it('splits phones and emails and sends raw base64 for both images', () => {
    const request = buildCreateRequest({ ...base, phones: '+20 1\n+20 2, +20 3', emails: 'a@x.test', tagline: 'Safety', logoBase64: 'LOGO', accreditationsBase64: 'ACC' });
    expect(request.companyProfile).toMatchObject({ phones: ['+20 1', '+20 2', '+20 3'], emails: ['a@x.test'], tagline: 'Safety', logoBase64: 'LOGO', accreditationsBase64: 'ACC' });
  });
});

describe('validateCompanyForm', () => {
  it('accepts a complete form', () => expect(validateCompanyForm(base)).toEqual([]));

  it('reports what is missing in plain words', () => {
    const problems = validateCompanyForm({ ...base, fullName: '', email: 'nope', password: 'short', name: '', address: '', primaryColor: 'red' });
    expect(problems).toEqual([
      "Enter the technical manager's full name.",
      "Enter the technical manager's email address.",
      'The password needs at least 12 characters.',
      'Enter the company name.',
      'Enter the company address.',
      'Colors must look like #RRGGBB.',
    ]);
  });

  it('does not ask for name and address when the built-in details are kept', () => {
    expect(validateCompanyForm({ ...base, useBuiltInDetails: true, name: '', address: '' })).toEqual([]);
  });

  it('refuses more than 5 phones', () => {
    expect(validateCompanyForm({ ...base, phones: '1,2,3,4,5,6' })).toEqual(['Give at most 5 phone numbers.']);
  });
});

describe('readPngAsBase64', () => {
  it('rejects non-PNG files and files over 1 MB without reading them', async () => {
    await expect(readPngAsBase64(new File(['x'], 'a.jpg', { type: 'image/jpeg' }))).rejects.toThrow('Choose a PNG image.');
    await expect(readPngAsBase64(new File([new Uint8Array(1024 * 1024 + 1)], 'big.png', { type: 'image/png' }))).rejects.toThrow('larger than 1 MB');
  });

  it('returns raw base64 without the data: prefix', async () => {
    expect(await readPngAsBase64(new File(['hi'], 'a.png', { type: 'image/png' }))).toBe('aGk=');
  });
});

it('splitList ignores blanks', () => expect(splitList(' a ,\n\n b;')).toEqual(['a', 'b']));
