'use client';

import { Form } from 'react-bootstrap';

interface Props {
    onChange(value: string): void;
    defaultValue?: any;
}

export function BooleanFilterElement({ onChange, defaultValue }: Props) {
    const isChecked = defaultValue === 'true';

    return (
        <Form.Check
            type="checkbox"
            label="Yes"
            checked={isChecked}
            onChange={(e) => onChange(e.target.checked ? 'true' : '')}
        />
    );
}
