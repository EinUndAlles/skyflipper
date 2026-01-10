'use client';

import { Form } from 'react-bootstrap';
import { getDecimalSeparator, getThousandSeparator } from '../../../utils/Formatter';

interface Props {
    onChange(value: string): void;
    options: FilterOptions;
    defaultValue?: any;
    isValid?: boolean;
}

export function EqualFilterElement({ onChange, options, defaultValue, isValid }: Props) {
    const _onChange = (selected: string) => {
        onChange(selected[0] || '');
    };

    const getOptionLabel = (option: string): string => {
        // Convert tag to name (e.g., SHARPNESS -> Sharpness)
        const baseName = option.replace(/_/g, ' ').replace(/\b\w/g, l => l.toUpperCase());
        return baseName;
    };

    return (
        <Form.Select
            value={defaultValue || ''}
            onChange={(e) => _onChange(e.target.value)}
            isInvalid={!isValid}
        >
            {options.options.map(opt => (
                <option key={opt} value={opt}>
                    {getOptionLabel(opt)}
                </option>
            ))}
        </Form.Select>
    );
}
